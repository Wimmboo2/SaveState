import type { SavedApp } from '@/lib/backup'

/** One saved app: name + publisher/version, and the user's note if they wrote one. */
export function AppRow({ app }: { app: SavedApp }) {
  const meta = [app.publisher, app.version && `v${app.version}`].filter(Boolean).join(', ')
  return (
    <div className="py-3.5">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-0.5">
        <p className="font-semibold text-ink">{app.name}</p>
        {meta && <p className="text-sm text-ink-muted">{meta}</p>}
      </div>
      {app.note && (
        <p className="mt-1.5 rounded-[10px] bg-surface-sunk px-3 py-2 text-sm leading-relaxed text-ink-muted">
          {app.note}
        </p>
      )}
    </div>
  )
}
