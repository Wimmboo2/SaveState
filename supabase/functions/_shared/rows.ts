// Shapes shared by the edge functions that write `public.backups`.

/** Columns returned to clients after a change. */
export const ROW_COLUMNS =
  'apps, file_path, size_bytes, uploaded_at, expires_at, updated_at, files, files_removed_at, files_removed_reason'

/** Clears the file columns and records why, so the app and website can say what happened. */
export const filesRemoved = (reason: 'deleted' | 'expired') => ({
  file_path: null,
  size_bytes: 0,
  uploaded_at: null,
  expires_at: null,
  files: null,
  files_removed_at: new Date().toISOString(),
  files_removed_reason: reason,
})
