import { MAX_BACKUP_BYTES } from '@/lib/constants'
import { formatBytes } from '@/lib/format'

/** "42 MB / 100 MB" with a calm fill. Turns amber past 85% so it's noticeable, not alarming. */
export function StorageBar({ used, max = MAX_BACKUP_BYTES }: { used: number; max?: number }) {
  const pct = Math.min(100, Math.round((used / max) * 100))
  const nearlyFull = pct >= 85
  return (
    <div>
      <div className="flex items-baseline justify-between gap-4 text-sm">
        <span className="font-semibold text-ink tabular-nums">
          {formatBytes(used)} <span className="font-normal text-ink-muted">/ {formatBytes(max)}</span>
        </span>
        <span className="text-ink-muted tabular-nums">{pct}% used</span>
      </div>
      <div
        className="mt-2 h-2.5 overflow-hidden rounded-full bg-surface-sunk"
        role="progressbar"
        aria-label="Backup storage used"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={pct}
      >
        <div
          className={`h-full rounded-full ${nearlyFull ? 'bg-warn' : 'bg-accent'}`}
          style={{ width: `${Math.max(pct, used > 0 ? 2 : 0)}%` }}
        />
      </div>
    </div>
  )
}
