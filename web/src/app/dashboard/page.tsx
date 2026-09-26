import type { Metadata } from 'next'
import { WindowsLogoIcon } from '@phosphor-icons/react/ssr'
import { AppRow } from '@/components/app-row'
import { buttonClass, cardClass } from '@/components/ui'
import { parseApps } from '@/lib/backup'
import { DESKTOP_DOWNLOAD_URL } from '@/lib/constants'
import { createClient } from '@/lib/supabase/server'

export const metadata: Metadata = { title: 'Dashboard' }

export default async function DashboardPage() {
  const supabase = await createClient()
  const { data: backup, error } = await supabase
    .from('backups')
    .select('apps, file_path, size_bytes, uploaded_at, expires_at')
    .maybeSingle()

  if (error) {
    return (
      <div role="alert" className={`${cardClass} p-8`}>
        <h1 className="font-display text-2xl font-bold">We couldn&apos;t load your backup</h1>
        <p className="mt-2 text-ink-muted">Refresh the page in a moment. If it keeps happening, log out and back in.</p>
      </div>
    )
  }

  const apps = parseApps(backup?.apps)

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

  return (
    <div className="grid gap-8">
      <h1 className="font-display text-3xl font-extrabold tracking-tight">Your backup</h1>
      <section className={`${cardClass} p-6 md:p-8`}>
        <h2 className="font-display text-xl font-bold">Saved apps</h2>
        <div className="mt-2 divide-y divide-line">
          {apps.map((app) => (
            <AppRow key={`${app.name}-${app.publisher}`} app={app} />
          ))}
        </div>
      </section>
    </div>
  )
}
