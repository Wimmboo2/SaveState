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
