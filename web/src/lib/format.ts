/** 43_991_040 → "42 MB". Uses binary units (like Windows Explorer) but the familiar labels. */
export function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  const units = ['KB', 'MB', 'GB']
  let value = bytes / 1024
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  const digits = value < 10 ? 1 : 0
  return `${value.toFixed(digits).replace(/\.0$/, '')} ${units[unit]}`
}

/** "in 23 days", "in 5 hours", "in under an hour", or "expired". */
export function formatTimeLeft(expiresAt: string | Date, now: Date = new Date()): string {
  const ms = new Date(expiresAt).getTime() - now.getTime()
  if (ms <= 0) return 'expired'
  const hours = ms / 36e5
  if (hours < 1) return 'in under an hour'
  if (hours < 24) {
    const h = Math.floor(hours)
    return `in ${h} ${h === 1 ? 'hour' : 'hours'}`
  }
  const days = Math.floor(hours / 24)
  return `in ${days} ${days === 1 ? 'day' : 'days'}`
}

export function formatDate(value: string | Date): string {
  return new Intl.DateTimeFormat('en', { day: 'numeric', month: 'short', year: 'numeric' }).format(
    new Date(value),
  )
}

/** True when the date is less than `days` away (or already past). */
export function isWithinDays(value: string | Date, days: number, now: Date = new Date()): boolean {
  return new Date(value).getTime() - now.getTime() < days * 24 * 60 * 60 * 1000
}

