// Keep in sync with web/src/lib/constants.ts and desktop/SaveState.Core/Config/AppConfig.cs
export const MAX_BACKUP_BYTES = 100 * 1024 * 1024 // 100 MB, one zip per user
export const EXPIRY_DAYS = 30 // zips are deleted this long after upload; the app list is kept

/** The one object key a user may have. Always derived from the verified JWT, never from input. */
export const backupKey = (userId: string) => `${userId}/backup.zip`

export const formatMB = (bytes: number) => `${Math.round(bytes / (1024 * 1024))} MB`
