// Keep in sync with desktop/SaveState.Core/Config/AppConfig.cs and supabase/functions/_shared/constants.ts
export const MAX_BACKUP_BYTES = 100 * 1024 * 1024 // 100 MB per user (one zip)
export const EXPIRY_DAYS = 30 // zipped files are deleted this long after upload; the app list is kept

export const GITHUB_URL = 'https://github.com/Wimmboo2/SaveState'
// Placeholder until the first desktop release is published (phase 8).
export const DESKTOP_DOWNLOAD_URL = `${GITHUB_URL}/releases/latest`
