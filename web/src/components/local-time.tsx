'use client'

/**
 * A date and time in the viewer's own time zone. The server renders in UTC, so the browser
 * re-formats it after hydration (the text can differ, hence suppressHydrationWarning).
 */
export function LocalTime({ value }: { value: string }) {
  const date = new Date(value)
  const text = new Intl.DateTimeFormat(undefined, {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date)
  return (
    <time dateTime={date.toISOString()} suppressHydrationWarning>
      {text}
    </time>
  )
}
