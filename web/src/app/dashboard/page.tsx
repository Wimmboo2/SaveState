import type { Metadata } from 'next'
import {
  CheckCircleIcon,
  ClockCountdownIcon,
  DownloadSimpleIcon,
  HourglassMediumIcon,
  TrashIcon,
  WarningCircleIcon,
  WindowsLogoIcon,
} from '@phosphor-icons/react/ssr'
import { StorageBar } from '@/components/storage-bar'
import { buttonClass, cardClass } from '@/components/ui'
import { type BackupContents, type FilesRemoved, parseApps, parseContents, parseRemoved } from '@/lib/backup'
import { callBackupFunction } from '@/lib/backup-function'
import { DESKTOP_DOWNLOAD_URL, EXPIRY_DAYS } from '@/lib/constants'
import { LocalTime } from '@/components/local-time'
import { formatBytes, formatDate, formatTimeLeft, isWithinDays } from '@/lib/format'
import { createClient } from '@/lib/supabase/server'
import { AppList } from './app-list'
import { DeleteBackup } from './delete-backup'
import { FileList } from './file-list'

export const metadata: Metadata = { title: 'Dashboard' }

// Messages for errors bounced back from /dashboard/download.
const DOWNLOAD_ERRORS: Record<string, string> = {
  expired: 'Your backup files have expired, so there was nothing to download. Your app list is still here.',
  no_files: 'There are no backup files to download right now.',
  storage_not_configured: "File storage isn't set up yet. Please try again later.",
  not_logged_in: 'Your login expired. Log in again and retry the download.',
}

const EXPIRES_SOON_DAYS = 5
const COLUMNS = 'apps, file_path, size_bytes, uploaded_at, expires_at, files, files_removed_at, files_removed_reason'

export default async function DashboardPage({ searchParams }: { searchParams: Promise<{ error?: string }> }) {
  const { error: errorCode } = await searchParams
  const supabase = await createClient()
  const { data: row, error } = await supabase.from('backups').select(COLUMNS).maybeSingle()
  let backup = row

  // Backups uploaded before file lists existed: have the server read the list out of the zip once.
  if (backup?.file_path && backup.files === null) {
    const filled = await callBackupFunction<{ backup: typeof backup }>('file-list')
    if (filled.ok && filled.data.backup) backup = filled.data.backup
  }

  if (error) {
    return (
      <div role="alert" className={`${cardClass} p-8`}>
        <h1 className="font-display text-2xl font-bold">We couldn&apos;t load your backup</h1>
        <p className="mt-2 text-ink-muted">
          Refresh the page in a moment. If it keeps happening, log out and back in.
        </p>
      </div>
    )
  }

  if (!backup) {
    return (
      <div className={`${cardClass} p-8 md:p-12`}>
        <h1 className="font-display text-3xl font-extrabold tracking-tight">No backup yet</h1>
        <p className="mt-3 max-w-[52ch] leading-relaxed text-ink-muted">
          Install the desktop app on the PC you&apos;re about to reinstall, log in with this account, and
          back up your apps and files. They&apos;ll show up here.
        </p>
        <a href={DESKTOP_DOWNLOAD_URL} className={buttonClass('primary', 'lg', 'mt-8')}>
          <WindowsLogoIcon size={20} weight="fill" aria-hidden />
          Download for Windows
        </a>
      </div>
    )
  }

  const apps = parseApps(backup.apps)
  const hasFiles = Boolean(backup.file_path && backup.expires_at)
  const contents = hasFiles ? parseContents(backup.files) : null
  const removed = hasFiles ? null : parseRemoved(backup.files_removed_at, backup.files_removed_reason)
  const errorMessage = errorCode ? (DOWNLOAD_ERRORS[errorCode] ?? 'The download failed. Please try again.') : null

  return (
    <div className="grid gap-8">
      <h1 className="font-display text-3xl font-extrabold tracking-tight md:text-4xl">Your backup</h1>

      {errorMessage && (
        <p role="alert" className="flex gap-2.5 rounded-[var(--radius-control)] bg-danger-soft px-4 py-3 text-ink">
          <WarningCircleIcon size={20} className="mt-0.5 shrink-0 text-danger" aria-hidden />
          {errorMessage}
        </p>
      )}

      <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        {/* Files first on phones: that's what people come back for. */}
        <aside className="lg:sticky lg:top-8 lg:order-2">
          {hasFiles ? (
            <FilesCard
              sizeBytes={backup.size_bytes}
              uploadedAt={backup.uploaded_at!}
              expiresAt={backup.expires_at!}
            />
          ) : (
            <NoFilesCard removed={removed} />
          )}
        </aside>

        <div className="grid gap-6 lg:order-1">
          {hasFiles && <ContentsSection contents={contents} />}

          <section aria-labelledby="apps-heading" className={`${cardClass} p-6 md:p-8`}>
            <h2 id="apps-heading" className="mb-3 font-display text-xl font-bold">
              Saved apps
            </h2>
            <AppList apps={apps} />
          </section>
        </div>
      </div>
    </div>
  )
}

function FilesCard({ sizeBytes, uploadedAt, expiresAt }: { sizeBytes: number; uploadedAt: string; expiresAt: string }) {
  const expiresSoon = isWithinDays(expiresAt, EXPIRES_SOON_DAYS)
  const left = formatTimeLeft(expiresAt)
  return (
    <section aria-labelledby="files-heading" className={`${cardClass} p-6`}>
      <p className="flex items-center gap-2 text-sm font-semibold text-accent">
        <CheckCircleIcon size={18} weight="fill" aria-hidden />
        Backed up
      </p>
      <h2 id="files-heading" className="mt-1 font-display text-xl font-bold">
        Backup files
      </h2>
      <p className="mt-1 text-sm text-ink-muted">
        Uploaded <LocalTime value={uploadedAt} />
      </p>

      <p
        className={`mt-5 flex items-center gap-2 rounded-[var(--radius-control)] px-3 py-2.5 text-sm font-semibold ${
          expiresSoon ? 'bg-warn-soft text-ink' : 'bg-accent-soft text-ink'
        }`}
      >
        <ClockCountdownIcon size={18} className={expiresSoon ? 'text-warn' : 'text-accent'} aria-hidden />
        {left === 'expired' ? 'Files have expired' : `Files expire ${left}`}
        <span className="font-normal text-ink-muted">({formatDate(expiresAt)})</span>
      </p>

      <div className="mt-5">
        <StorageBar used={sizeBytes} />
      </div>

      <div className="mt-6 grid gap-2">
        {/* A plain link: the route handler redirects to a 60-second signed download URL. */}
        <a href="/dashboard/download" className={buttonClass('primary', 'lg', 'w-full')}>
          <DownloadSimpleIcon size={20} weight="bold" aria-hidden />
          Download backup
        </a>
        <DeleteBackup sizeLabel={formatBytes(sizeBytes)} />
      </div>
      <p className="mt-4 text-sm leading-relaxed text-ink-muted">
        Unzip it and open README.txt. It says where every file goes back.
      </p>
    </section>
  )
}

function ContentsSection({ contents }: { contents: BackupContents | 'unavailable' | null }) {
  return (
    <section aria-labelledby="contents-heading" className={`${cardClass} p-6 md:p-8`}>
      <h2 id="contents-heading" className="mb-3 font-display text-xl font-bold">
        Files in your backup
      </h2>
      {contents === 'unavailable' || contents === null ? (
        <p className="py-4 leading-relaxed text-ink-muted">
          {contents === null
            ? "The file list isn't ready yet. Refresh the page in a moment."
            : "This backup doesn't include a file list. Download it and open README.txt to see what's inside, or back up again from the desktop app."}
        </p>
      ) : (
        <FileList contents={contents} />
      )}
    </section>
  )
}

function NoFilesCard({ removed }: { removed: FilesRemoved | null }) {
  const deleted = removed?.reason === 'deleted'
  const Icon = deleted ? TrashIcon : HourglassMediumIcon
  const title = !removed ? 'No files stored right now' : deleted ? 'Backup files deleted' : 'Backup files expired'
  const detail = !removed ? (
    <>Files are removed {EXPIRY_DAYS} days after upload or when you delete them. Your app list is always kept.</>
  ) : deleted ? (
    <>
      You deleted your backup files on <LocalTime value={removed.at} />. Your app list is still saved.
    </>
  ) : (
    <>
      Your backup files were removed on <LocalTime value={removed.at} />, {EXPIRY_DAYS} days after upload. Your app
      list is still saved.
    </>
  )
  return (
    <section aria-labelledby="nofiles-heading" className={`${cardClass} p-6`}>
      <span className="grid size-11 place-items-center rounded-full bg-surface-sunk text-ink-muted">
        <Icon size={22} aria-hidden />
      </span>
      <h2 id="nofiles-heading" className="mt-4 font-display text-xl font-bold">
        {title}
      </h2>
      <p className="mt-2 text-sm leading-relaxed text-ink-muted">
        {detail} Back up again from the desktop app to store new files.
      </p>
      <a href={DESKTOP_DOWNLOAD_URL} className={buttonClass('secondary', 'md', 'mt-5 w-full')}>
        <WindowsLogoIcon size={18} weight="fill" aria-hidden />
        Get the desktop app
      </a>
    </section>
  )
}
