// SaveState `backup` edge function.
//
// The only thing that talks to the private R2 bucket on behalf of users. It checks the caller's
// Supabase login, then hands out short-lived presigned URLs for exactly one object:
// `{user_id}/backup.zip`. File metadata in `public.backups` (file_path, size_bytes, uploaded_at,
// expires_at) is written only here (and by cleanup-expired), based on what's really in storage.
//
// POST { action: "upload-url", size_bytes }  -> { url, expires_in, max_bytes }
// POST { action: "confirm-upload" }          -> { backup }   (also reads the zip's file list)
// POST { action: "file-list" }               -> { backup }   (fills in a missing file list)
// POST { action: "download-url" }            -> { url, file_name, size_bytes, expires_in }
// POST { action: "delete" }                  -> { backup }
//
// `files` (what's inside the zip) is read here from the zip's own manifest.json with a few ranged
// reads, so the list always matches what was really uploaded. When files go away,
// `files_removed_at` / `files_removed_reason` record when and why.
//
// Deployed with verify_jwt = false: the gateway check doesn't support the project's asymmetric
// JWT signing keys, so the token is verified here with auth.getClaims() instead (same guarantee).

import 'jsr:@supabase/functions-js/edge-runtime.d.ts'
import { adminClient } from '../_shared/admin.ts'
import { backupKey, EXPIRY_DAYS, formatMB, MAX_BACKUP_BYTES } from '../_shared/constants.ts'
import { fileListFromZip } from '../_shared/file-list.ts'
import { fail, json } from '../_shared/http.ts'
import { head, presignDownload, presignUpload, r2, rangeReader, remove } from '../_shared/r2.ts'
import { filesRemoved, ROW_COLUMNS } from '../_shared/rows.ts'

Deno.serve(async (req) => {
  if (req.method !== 'POST') return fail(405, 'method_not_allowed', 'Use POST.')

  const admin = adminClient()

  // Who is calling? Must be a logged-in user, not the bare publishable key.
  const token = req.headers.get('Authorization')?.replace(/^Bearer\s+/i, '')
  if (!token) return fail(401, 'not_logged_in', 'Please log in again.')
  const { data: claimsData, error: claimsError } = await admin.auth.getClaims(token)
  const userId = claimsData?.claims?.sub
  if (claimsError || !userId || claimsData?.claims?.role !== 'authenticated') {
    return fail(401, 'not_logged_in', 'Your login has expired. Please log in again.')
  }

  let body: { action?: string; size_bytes?: unknown }
  try {
    body = await req.json()
  } catch {
    return fail(400, 'bad_request', 'The request was not valid JSON.')
  }

  const storage = r2()
  if (!storage) {
    console.error('R2 secrets missing: set R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET')
    return fail(503, 'storage_not_configured', "File storage isn't set up yet. Please try again later.")
  }

  const key = backupKey(userId)

  try {
    switch (body.action) {
      case 'upload-url': {
        const size = Number(body.size_bytes)
        if (!Number.isSafeInteger(size) || size <= 0) {
          return fail(400, 'bad_size', 'The backup size was missing or invalid.')
        }
        if (size > MAX_BACKUP_BYTES) {
          return fail(
            413,
            'too_large',
            `Your backup is ${formatMB(size)}, over the ${formatMB(MAX_BACKUP_BYTES)} limit. Remove ${formatMB(size - MAX_BACKUP_BYTES)} of files and try again.`,
          )
        }
        const expiresIn = 600
        const url = await presignUpload(storage, key, size, expiresIn)
        return json({ url, expires_in: expiresIn, max_bytes: MAX_BACKUP_BYTES })
      }

      case 'confirm-upload': {
        const object = await head(storage, key)
        if (!object) {
          return fail(404, 'not_uploaded', "The upload didn't arrive. Please try backing up again.")
        }
        if (object.size > MAX_BACKUP_BYTES) {
          await remove(storage, key)
          return fail(413, 'too_large', `That upload was over the ${formatMB(MAX_BACKUP_BYTES)} limit and was removed.`)
        }
        const uploadedAt = object.lastModified
        const expiresAt = new Date(uploadedAt.getTime() + EXPIRY_DAYS * 24 * 60 * 60 * 1000)
        const files = await fileListFromZip(rangeReader(storage, key), object.size)
        const { data, error } = await admin
          .from('backups')
          .upsert(
            {
              user_id: userId,
              file_path: key,
              size_bytes: object.size,
              uploaded_at: uploadedAt.toISOString(),
              expires_at: expiresAt.toISOString(),
              files,
              files_removed_at: null,
              files_removed_reason: null,
            },
            { onConflict: 'user_id' },
          )
          .select(ROW_COLUMNS)
          .single()
        if (error) throw error
        return json({ backup: data })
      }

      case 'download-url': {
        const { data: row, error } = await admin
          .from('backups')
          .select('file_path, uploaded_at')
          .eq('user_id', userId)
          .maybeSingle()
        if (error) throw error
        if (!row?.file_path) {
          return fail(404, 'no_files', "There are no backup files to download. They're removed 30 days after upload.")
        }
        const object = await head(storage, key)
        if (!object) {
          // Storage already dropped it (e.g. the bucket's lifecycle rule): keep the row honest.
          await admin.from('backups').update(filesRemoved('expired')).eq('user_id', userId)
          return fail(410, 'expired', 'Your backup files have expired. Your app list is still saved.')
        }
        const date = new Date(row.uploaded_at ?? object.lastModified).toISOString().slice(0, 10)
        const fileName = `SaveState-backup-${date}.zip`
        const expiresIn = 60
        const url = await presignDownload(storage, key, fileName, expiresIn)
        return json({ url, file_name: fileName, size_bytes: object.size, expires_in: expiresIn })
      }

      case 'file-list': {
        // For backups uploaded before file lists existed (or if reading it failed at upload).
        const { data: row, error } = await admin
          .from('backups')
          .select(ROW_COLUMNS)
          .eq('user_id', userId)
          .maybeSingle()
        if (error) throw error
        if (!row?.file_path || row.files) return json({ backup: row })
        const object = await head(storage, key)
        const update = object
          ? { files: await fileListFromZip(rangeReader(storage, key), object.size) }
          : filesRemoved('expired')
        const { data, error: updateError } = await admin
          .from('backups')
          .update(update)
          .eq('user_id', userId)
          .eq('uploaded_at', row.uploaded_at) // not replaced by a newer upload meanwhile
          .is('files', null)
          .select(ROW_COLUMNS)
          .maybeSingle()
        if (updateError) throw updateError
        return json({ backup: data ?? row })
      }

      case 'delete': {
        await remove(storage, key)
        const { data: row, error } = await admin
          .from('backups')
          .select('file_path')
          .eq('user_id', userId)
          .maybeSingle()
        if (error) throw error
        // Only record a deletion if there was something to delete (keeps the first date).
        if (row?.file_path) {
          const { error: updateError } = await admin.from('backups').update(filesRemoved('deleted')).eq('user_id', userId)
          if (updateError) throw updateError
        }
        const { data, error: readError } = await admin
          .from('backups')
          .select(ROW_COLUMNS)
          .eq('user_id', userId)
          .maybeSingle()
        if (readError) throw readError
        return json({ backup: data ?? { apps: [], ...filesRemoved('deleted'), files_removed_at: null, files_removed_reason: null } })
      }

      default:
        return fail(400, 'unknown_action', 'Unknown action.')
    }
  } catch (e) {
    console.error(`backup ${body.action} failed for ${userId}:`, e)
    return fail(500, 'server_error', 'Something went wrong on our side. Please try again in a moment.')
  }
})
