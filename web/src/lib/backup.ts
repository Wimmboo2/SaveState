import type { Json } from './database.types'

export type SavedApp = {
  name: string
  publisher: string | null
  version: string | null
  note: string | null
}

/** The `apps` column is jsonb written by the desktop app; parse defensively. */
export function parseApps(value: Json | null | undefined): SavedApp[] {
  if (!Array.isArray(value)) return []
  return value
    .filter((item): item is { [key: string]: Json | undefined } => !!item && typeof item === 'object' && !Array.isArray(item))
    .map((item) => ({
      name: typeof item.name === 'string' ? item.name : '',
      publisher: typeof item.publisher === 'string' && item.publisher ? item.publisher : null,
      version: typeof item.version === 'string' && item.version ? item.version : null,
      note: typeof item.note === 'string' && item.note.trim() ? item.note.trim() : null,
    }))
    .filter((app) => app.name.length > 0)
    .sort((a, b) => a.name.localeCompare(b.name, undefined, { sensitivity: 'base' }))
}

export type BackupFile = { path: string; sizeBytes: number }
export type BackupItem = {
  path: string
  kind: 'file' | 'folder'
  fileCount: number
  sizeBytes: number
  /** Relative to the folder. May be fewer than fileCount on huge backups. */
  files: BackupFile[]
}
export type BackupContents = {
  fileCount: number
  totalBytes: number
  itemCount: number
  listedCount: number
  items: BackupItem[]
}

/**
 * The `files` column: a summary of the zip written by the backup edge function from the zip's own
 * manifest. `unavailable` when the zip has no readable manifest; null when there's no list yet.
 */
export function parseContents(value: Json | null | undefined): BackupContents | 'unavailable' | null {
  if (!isRecord(value)) return null
  if (value.unavailable === true) return 'unavailable'
  if (!Array.isArray(value.items)) return null
  const items = value.items.filter(isRecord).map(
    (item): BackupItem => ({
      path: str(item.path),
      kind: item.kind === 'folder' ? 'folder' : 'file',
      fileCount: num(item.file_count),
      sizeBytes: num(item.size_bytes),
      files: (Array.isArray(item.files) ? item.files : [])
        .filter(isRecord)
        .map((f) => ({ path: str(f.path), sizeBytes: num(f.size_bytes) }))
        .filter((f) => f.path),
    }),
  )
  return {
    fileCount: num(value.file_count),
    totalBytes: num(value.total_bytes),
    itemCount: num(value.item_count) || items.length,
    listedCount: num(value.listed_count),
    items: items.filter((i) => i.path),
  }
}

export type FilesRemoved = { at: string; reason: 'deleted' | 'expired' }

export function parseRemoved(at: string | null, reason: string | null): FilesRemoved | null {
  if (!at || (reason !== 'deleted' && reason !== 'expired')) return null
  return { at, reason }
}

function isRecord(value: unknown): value is { [key: string]: Json | undefined } {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function str(value: unknown) {
  return typeof value === 'string' ? value : ''
}

function num(value: unknown) {
  return typeof value === 'number' && Number.isFinite(value) && value >= 0 ? value : 0
}
