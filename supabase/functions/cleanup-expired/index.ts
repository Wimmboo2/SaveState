// SaveState `cleanup-expired` edge function. Called once a day by pg_cron (see the cleanup_cron
// migration) with an `x-cron-secret` header that must match the Vault secret.
//
// 1. Expired rows: `expires_at < now()` with a file_path → delete the zip in R2 (through the
//    storage API, so the bytes are really gone) → clear the row's file columns and mark them
//    'expired' (the app and website show when). The app list stays.
// 2. Sweep: anything left in the bucket that's past 30 days, over the size limit, or not referenced
//    by any row for more than a day (abandoned upload, deleted account) is deleted too.
//
// Uses the service role key, which only exists in this function's server-side environment.

import 'jsr:@supabase/functions-js/edge-runtime.d.ts'
import { adminClient } from '../_shared/admin.ts'
import { EXPIRY_DAYS, MAX_BACKUP_BYTES } from '../_shared/constants.ts'
import { fail, json } from '../_shared/http.ts'
import { listAll, r2, remove } from '../_shared/r2.ts'
import { filesRemoved } from '../_shared/rows.ts'

const DAY_MS = 24 * 60 * 60 * 1000

Deno.serve(async (req) => {
  if (req.method !== 'POST') return fail(405, 'method_not_allowed', 'Use POST.')

  const admin = adminClient()
  const secret = req.headers.get('x-cron-secret') ?? ''
  const { data: valid, error: secretError } = secret
    ? await admin.rpc('verify_cleanup_secret', { candidate: secret })
    : { data: false, error: null }
  if (secretError) console.error('secret check failed:', secretError)
  if (valid !== true) return fail(401, 'unauthorized', 'Unauthorized.')

  const storage = r2()
  if (!storage) return fail(503, 'storage_not_configured', 'R2 secrets are not set.')

  const now = new Date()
  const nowIso = now.toISOString()
  const summary = { expired: 0, swept: 0, errors: 0 }

  // 1. Rows whose files have expired.
  const { data: expired, error } = await admin
    .from('backups')
    .select('user_id, file_path')
    .lt('expires_at', nowIso)
    .not('file_path', 'is', null)
  if (error) {
    console.error('listing expired rows failed:', error)
    return fail(500, 'server_error', 'Listing expired backups failed.')
  }

  for (const row of expired ?? []) {
    try {
      await remove(storage, row.file_path!)
      // Only clear if nothing changed meanwhile (e.g. the user re-uploaded a second ago).
      const { error: updateError } = await admin
        .from('backups')
        .update(filesRemoved('expired'))
        .eq('user_id', row.user_id)
        .eq('file_path', row.file_path!)
        .lt('expires_at', nowIso)
      if (updateError) throw updateError
      summary.expired++
    } catch (e) {
      summary.errors++
      console.error(`expiring ${row.file_path} failed:`, e)
    }
  }

  // 2. Sweep the bucket for anything the rows don't account for.
  const { data: live, error: liveError } = await admin
    .from('backups')
    .select('user_id, file_path')
    .not('file_path', 'is', null)
  if (liveError) {
    console.error('listing live rows failed:', liveError)
    return json({ ok: false, ...summary })
  }
  const referenced = new Set((live ?? []).map((r) => r.file_path))

  try {
    for await (const object of listAll(storage)) {
      const age = now.getTime() - object.lastModified.getTime()
      const tooOld = age > EXPIRY_DAYS * DAY_MS + DAY_MS / 24 // one hour of grace
      const tooBig = object.size > MAX_BACKUP_BYTES
      const orphaned = !referenced.has(object.key) && age > DAY_MS
      if (!tooOld && !tooBig && !orphaned) continue
      try {
        await remove(storage, object.key)
        if (referenced.has(object.key)) {
          await admin.from('backups').update(filesRemoved('expired')).eq('file_path', object.key)
        }
        summary.swept++
        console.log(`swept ${object.key} (${tooOld ? 'old' : tooBig ? 'oversized' : 'orphaned'})`)
      } catch (e) {
        summary.errors++
        console.error(`sweeping ${object.key} failed:`, e)
      }
    }
  } catch (e) {
    summary.errors++
    console.error('listing bucket failed:', e)
  }

  console.log('cleanup done', summary)
  return json({ ok: summary.errors === 0, ...summary })
})
