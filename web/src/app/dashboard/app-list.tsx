'use client'

import { useDeferredValue, useState } from 'react'
import { MagnifyingGlassIcon } from '@phosphor-icons/react'
import { AppRow } from '@/components/app-row'
import { inputClass } from '@/components/ui'
import type { SavedApp } from '@/lib/backup'

/** Searchable saved-app list (name, publisher or note). */
export function AppList({ apps }: { apps: SavedApp[] }) {
  const [query, setQuery] = useState('')
  const deferred = useDeferredValue(query)
  const q = deferred.trim().toLowerCase()
  const visible = q
    ? apps.filter((a) => [a.name, a.publisher, a.note].some((v) => v?.toLowerCase().includes(q)))
    : apps
  const withNotes = apps.filter((a) => a.note).length

  if (apps.length === 0) {
    return (
      <p className="py-6 leading-relaxed text-ink-muted">
        No apps saved yet. In the desktop app, tick the apps you want to remember on the Apps page and
        back up.
      </p>
    )
  }

  return (
    <div>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <p className="text-sm text-ink-muted">
          {apps.length} {apps.length === 1 ? 'app' : 'apps'}
          {withNotes > 0 && `, ${withNotes} with notes`}
        </p>
        <div className="relative sm:w-72">
          <label htmlFor="app-search" className="sr-only">
            Search saved apps
          </label>
          <MagnifyingGlassIcon
            size={18}
            className="pointer-events-none absolute top-1/2 left-3.5 -translate-y-1/2 text-ink-subtle"
            aria-hidden
          />
          <input
            id="app-search"
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search apps or notes"
            className={`${inputClass} pl-10`}
          />
        </div>
      </div>

      <div className="mt-2 divide-y divide-line" aria-live="polite">
        {visible.map((app) => (
          <AppRow key={`${app.name}|${app.publisher}`} app={app} />
        ))}
        {visible.length === 0 && (
          <p className="py-8 text-center text-ink-muted">No saved apps match &ldquo;{deferred}&rdquo;.</p>
        )}
      </div>
    </div>
  )
}
