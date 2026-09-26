'use client'

import { useDeferredValue, useState } from 'react'
import { CaretRightIcon, FileIcon, FolderSimpleIcon, MagnifyingGlassIcon } from '@phosphor-icons/react'
import { inputClass } from '@/components/ui'
import type { BackupContents, BackupItem } from '@/lib/backup'
import { formatBytes } from '@/lib/format'

const MAX_SEARCH_RESULTS = 400

/** Everything inside the backup zip: each picked file or folder, and the files in each folder. */
export function FileList({ contents }: { contents: BackupContents }) {
  const [query, setQuery] = useState('')
  const [open, setOpen] = useState<Set<number>>(() => new Set())
  const deferred = useDeferredValue(query)
  const q = deferred.trim().toLowerCase()

  const toggle = (index: number) =>
    setOpen((prev) => {
      const next = new Set(prev)
      if (next.has(index)) next.delete(index)
      else next.add(index)
      return next
    })

  // While searching, show the items whose path matches or that contain matching files.
  const rows: { item: BackupItem; index: number; files: BackupItem['files'] }[] = []
  let shownFiles = 0
  contents.items.forEach((item, index) => {
    if (!q) {
      rows.push({ item, index, files: item.files })
      return
    }
    const itemMatch = item.path.toLowerCase().includes(q)
    const matches = itemMatch ? item.files : item.files.filter((f) => f.path.toLowerCase().includes(q))
    if (!itemMatch && matches.length === 0) return
    const files = matches.slice(0, Math.max(0, MAX_SEARCH_RESULTS - shownFiles))
    shownFiles += files.length
    rows.push({ item, index, files })
  })

  const hiddenItems = contents.itemCount - contents.items.length

  return (
    <div>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-ink-muted">
          {contents.fileCount.toLocaleString('en')} {contents.fileCount === 1 ? 'file' : 'files'} from{' '}
          {contents.itemCount} picked {contents.itemCount === 1 ? 'item' : 'items'},{' '}
          {formatBytes(contents.totalBytes)} before zipping
        </p>
        <div className="relative sm:w-72">
          <label htmlFor="file-search" className="sr-only">
            Search backed up files
          </label>
          <MagnifyingGlassIcon
            size={18}
            className="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-ink-subtle"
            aria-hidden
          />
          <input
            id="file-search"
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search files"
            className={`${inputClass} pl-10`}
          />
        </div>
      </div>

      <ul className="mt-2 divide-y divide-line" aria-live="polite">
        {rows.map(({ item, index, files }) => (
          <ItemRow
            key={index}
            item={item}
            files={files}
            expanded={q ? files.length > 0 : open.has(index)}
            onToggle={q ? undefined : () => toggle(index)}
          />
        ))}
        {rows.length === 0 && (
          <li className="py-8 text-center text-ink-muted">No backed up files match &ldquo;{deferred}&rdquo;.</li>
        )}
      </ul>

      {q && shownFiles >= MAX_SEARCH_RESULTS && (
        <p className="mt-3 text-sm text-ink-muted">Showing the first {MAX_SEARCH_RESULTS} matches. Try a longer search.</p>
      )}
      {hiddenItems > 0 && (
        <p className="mt-3 text-sm text-ink-muted">
          {hiddenItems} more {hiddenItems === 1 ? 'item is' : 'items are'} in the zip but not listed here.
        </p>
      )}
      <p className="mt-5 text-sm leading-relaxed text-ink-muted">
        Paths like <code className="font-mono text-[0.85em] text-ink">%APPDATA%</code> are Windows folders: paste
        one into File Explorer&apos;s address bar to open it.
      </p>
    </div>
  )
}

function ItemRow({
  item,
  files,
  expanded,
  onToggle,
}: {
  item: BackupItem
  files: BackupItem['files']
  expanded: boolean
  onToggle?: () => void
}) {
  const isFolder = item.kind === 'folder'
  const meta = isFolder
    ? `${item.fileCount.toLocaleString('en')} ${item.fileCount === 1 ? 'file' : 'files'}, ${formatBytes(item.sizeBytes)}`
    : formatBytes(item.sizeBytes)
  const Icon = isFolder ? FolderSimpleIcon : FileIcon
  const unlisted = item.fileCount - item.files.length

  const label = (
    <>
      <Icon size={20} weight={isFolder ? 'fill' : 'regular'} className="mt-0.5 shrink-0 text-accent" aria-hidden />
      <span className="min-w-0 flex-1">
        <span className="font-mono text-sm break-words text-ink">
          <BreakablePath path={item.path} />
        </span>
        <span className="mt-0.5 block text-sm text-ink-muted sm:hidden">{meta}</span>
      </span>
      <span className="hidden shrink-0 text-sm whitespace-nowrap text-ink-muted sm:block">{meta}</span>
    </>
  )

  return (
    <li className="py-2">
      {isFolder && onToggle ? (
        <button
          type="button"
          onClick={onToggle}
          aria-expanded={expanded}
          className="flex w-full items-start gap-3 rounded-[var(--radius-control)] px-2 py-1.5 text-left transition-colors hover:bg-surface-sunk"
        >
          <CaretRightIcon
            size={14}
            weight="bold"
            className={`mt-1 shrink-0 text-ink-subtle transition-transform duration-200 ${expanded ? 'rotate-90' : ''}`}
            aria-hidden
          />
          {label}
        </button>
      ) : (
        /* Lines up with the icon of expandable rows (button padding + caret + gap). */
        <div className="flex items-start gap-3 py-1.5 pr-2 pl-[2.125rem]">{label}</div>
      )}

      {isFolder && expanded && (
        <ul className="mt-1 mb-2 ml-[2.4rem] border-l border-line pl-4">
          {files.map((file) => (
            <li key={file.path} className="flex items-baseline gap-3 py-1">
              <span className="min-w-0 flex-1 font-mono text-[0.8rem] break-words text-ink-muted">
                <BreakablePath path={file.path} />
              </span>
              <span className="shrink-0 text-xs whitespace-nowrap text-ink-subtle">{formatBytes(file.sizeBytes)}</span>
            </li>
          ))}
          {onToggle && unlisted > 0 && (
            <li className="py-1 text-xs text-ink-subtle">
              and {unlisted.toLocaleString('en')} more {unlisted === 1 ? 'file' : 'files'} (the list is cut short on very big
              backups)
            </li>
          )}
        </ul>
      )}
    </li>
  )
}

/** Lets long paths wrap after a backslash instead of in the middle of a folder name. */
function BreakablePath({ path }: { path: string }) {
  const parts = path.split('\\')
  return parts.map((part, i) => (
    <span key={i}>
      {part}
      {i < parts.length - 1 && (
        <>
          {'\\'}
          <wbr />
        </>
      )}
    </span>
  ))
}
