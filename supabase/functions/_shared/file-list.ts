// Turns the desktop app's manifest.json (inside the zip) into the compact file list stored in
// `backups.files`, which the website and the app show as "what's in your backup".
// The manifest comes from the user's own zip, so it's parsed defensively and capped.

import { readZipEntry, type RangeReader } from './zip.ts'

export const MAX_LISTED_FILES = 5000
const MAX_ITEMS = 2000
const MAX_PATH = 1024
// The column allows 2 MB (backups_files_size); stay well under it even with long, non-ASCII paths.
const MAX_JSON_BYTES = 1_500_000

export type ListedFile = { path: string; size_bytes: number }
export type ListedItem = {
  path: string
  kind: 'file' | 'folder'
  file_count: number
  size_bytes: number
  /** Paths relative to the folder (folders only). May be cut short, see listed_count. */
  files: ListedFile[]
}
export type FileList =
  | {
      file_count: number
      total_bytes: number
      /** Items the user picked; `items` may list fewer if the list got too long. */
      item_count: number
      /** Files shown across `items` (file items count as one). */
      listed_count: number
      items: ListedItem[]
    }
  | { unavailable: true }

export const UNAVAILABLE: FileList = { unavailable: true }

/** Reads manifest.json out of the zip and summarises it. Never throws: bad zips are "unavailable". */
export async function fileListFromZip(read: RangeReader, zipSize: number): Promise<FileList> {
  try {
    const bytes = await readZipEntry(read, zipSize, 'manifest.json')
    if (!bytes) return UNAVAILABLE
    return summarizeManifest(JSON.parse(new TextDecoder().decode(bytes)))
  } catch (e) {
    console.warn('could not read the backup manifest:', e instanceof Error ? e.message : e)
    return UNAVAILABLE
  }
}

/**
 * manifest.items is what the user picked (in order); manifest.files lists every file, written
 * item by item, so each item's files are the next `fileCount` entries.
 */
export function summarizeManifest(manifest: unknown): FileList {
  if (!isObject(manifest) || !Array.isArray(manifest.items) || !Array.isArray(manifest.files)) return UNAVAILABLE
  const files = manifest.files.filter(isObject)

  const encoder = new TextEncoder()
  const cost = (path: string) => encoder.encode(path).length + 48 // + JSON keys, quotes and the size
  const items: ListedItem[] = []
  let next = 0
  let listed = 0
  let budget = MAX_JSON_BYTES
  let totalBytes = 0
  let totalFiles = 0
  let itemCount = 0

  for (const raw of manifest.items.filter(isObject).slice(0, MAX_ITEMS)) {
    const path = str(raw.originalPath)
    if (!path) continue
    const kind = raw.kind === 'folder' ? 'folder' : 'file'
    const fileCount = int(raw.fileCount)
    const sizeBytes = int(raw.sizeBytes)
    const own = files.slice(next, next + fileCount)
    next += fileCount
    totalFiles += fileCount
    totalBytes += sizeBytes
    itemCount++

    // Totals always cover everything; the listing stops when it would get too big.
    if (budget < cost(path) + 64) continue
    budget -= cost(path) + 64
    const item: ListedItem = { path, kind, file_count: fileCount, size_bytes: sizeBytes, files: [] }
    if (kind === 'folder') {
      const base = path.toLowerCase().replace(/[\\/]+$/, '')
      const entries = own
        .map((f) => {
          const full = str(f.originalPath)
          const inside = full.toLowerCase().startsWith(base) && /[\\/]/.test(full.charAt(base.length))
          return { path: inside ? full.slice(base.length + 1) : full, size_bytes: int(f.sizeBytes) }
        })
        .filter((f) => f.path)
        .sort((a, b) => a.path.localeCompare(b.path, undefined, { sensitivity: 'base', numeric: true }))
      for (const entry of entries) {
        if (listed >= MAX_LISTED_FILES || budget < cost(entry.path)) break
        budget -= cost(entry.path)
        item.files.push(entry)
        listed++
      }
    } else {
      listed++
    }
    items.push(item)
  }

  return { file_count: totalFiles, total_bytes: totalBytes, item_count: itemCount, listed_count: listed, items }
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function str(value: unknown) {
  return typeof value === 'string' ? value.slice(0, MAX_PATH) : ''
}

function int(value: unknown) {
  return typeof value === 'number' && Number.isSafeInteger(value) && value >= 0 ? value : 0
}
