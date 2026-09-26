import Link from 'next/link'
import {
  ArrowsClockwiseIcon,
  DownloadSimpleIcon,
  ListChecksIcon,
  ShieldCheckIcon,
  WindowsLogoIcon,
} from '@phosphor-icons/react/ssr'
import { AppRow } from '@/components/app-row'
import { SiteFooter } from '@/components/site-footer'
import { SiteHeader } from '@/components/site-header'
import { StorageBar } from '@/components/storage-bar'
import { buttonClass, cardClass } from '@/components/ui'
import type { SavedApp } from '@/lib/backup'
import { DESKTOP_DOWNLOAD_URL, EXPIRY_DAYS } from '@/lib/constants'
import { createClient } from '@/lib/supabase/server'

// Sample data for the dashboard preview in the hero. Rendered with the real dashboard components.
const SAMPLE_APPS: SavedApp[] = [
  { name: 'Discord', publisher: 'Discord Inc.', version: '1.0.9214', note: null },
  {
    name: 'Minecraft Launcher',
    publisher: 'Mojang',
    version: '2.24.17',
    note: 'Fabric 0.16 for 1.21.4, then drop the mods folder back in.',
  },
  { name: 'OBS Studio', publisher: 'OBS Project', version: '31.1.2', note: 'Scenes export is on the desktop.' },
  { name: 'Steam', publisher: 'Valve Corporation', version: '2.10.91.91', note: null },
]

const STEPS = [
  {
    icon: ListChecksIcon,
    title: 'Back up',
    body: 'Open the desktop app, tick the apps you want to remember, jot down notes, and pick the folders worth keeping.',
  },
  {
    icon: ArrowsClockwiseIcon,
    title: 'Reinstall',
    body: 'Reset or reinstall Windows however you like. Your backup waits in your account.',
  },
  {
    icon: DownloadSimpleIcon,
    title: 'Download',
    body: 'Log in here, check your app list, and grab your zip. The README inside says where every file goes.',
  },
]

export default async function HomePage() {
  const supabase = await createClient()
  const { data } = await supabase.auth.getClaims()
  const isLoggedIn = Boolean(data?.claims?.sub)

  return (
    <>
      <SiteHeader isLoggedIn={isLoggedIn} />

      <main>
        {/* Hero: split, copy left, real dashboard preview right */}
        <section className="mx-auto grid w-full max-w-6xl items-center gap-12 px-4 pt-10 pb-20 sm:px-6 md:pt-16 lg:grid-cols-[minmax(0,1.05fr)_minmax(0,1fr)] lg:gap-16 lg:pt-20 lg:pb-28">
          <div>
            <h1 className="font-display text-4xl leading-[1.08] font-extrabold tracking-tight text-balance sm:text-5xl lg:text-6xl">
              Reinstall Windows. Keep your setup.
            </h1>
            <p className="mt-6 max-w-[46ch] text-lg leading-relaxed text-pretty text-ink-muted">
              SaveState remembers your apps and backs up the small files that make your PC yours, like
              game configs and mods.
            </p>
            <div className="mt-9 flex flex-col gap-3 sm:flex-row">
              <a href={DESKTOP_DOWNLOAD_URL} className={buttonClass('primary', 'lg')}>
                <WindowsLogoIcon size={20} weight="fill" aria-hidden />
                Download for Windows
              </a>
              {!isLoggedIn && (
                <Link href="/signup" className={buttonClass('secondary', 'lg')}>
                  Create account
                </Link>
              )}
            </div>
          </div>

          <figure className="animate-settle-in relative">
            <div className={`${cardClass} p-5 shadow-lift sm:p-7`}>
              <div className="flex items-center justify-between gap-4">
                <p className="font-display text-lg font-bold">Your saved apps</p>
                <p className="text-sm text-ink-muted">Files expire in 23 days</p>
              </div>
              <div className="mt-2 divide-y divide-line">
                {SAMPLE_APPS.map((app) => (
                  <AppRow key={app.name} app={app} />
                ))}
              </div>
              <div className="mt-4 border-t border-line pt-5">
                <StorageBar used={44_040_192} />
              </div>
            </div>
            <figcaption className="mt-3 text-center text-sm text-ink-subtle">
              A sample dashboard after a backup.
            </figcaption>
          </figure>
        </section>

        {/* How it works: headline left, verb-led steps right */}
        <section className="border-y border-line bg-surface">
          <div className="mx-auto grid w-full max-w-6xl gap-10 px-4 py-20 sm:px-6 md:grid-cols-[minmax(0,1fr)_minmax(0,1.6fr)] md:gap-16 md:py-24">
            <h2 className="self-start font-display text-3xl font-extrabold tracking-tight text-balance md:sticky md:top-10 md:text-4xl">
              How it works
            </h2>
            <ol className="grid gap-10">
              {STEPS.map(({ icon: Icon, title, body }) => (
                <li key={title} className="grid grid-cols-[auto_1fr] gap-x-5">
                  <span className="grid size-12 place-items-center rounded-full bg-accent-soft text-accent">
                    <Icon size={24} weight="duotone" aria-hidden />
                  </span>
                  <div>
                    <h3 className="font-display text-xl font-bold">{title}</h3>
                    <p className="mt-1.5 max-w-[52ch] leading-relaxed text-ink-muted">{body}</p>
                  </div>
                </li>
              ))}
            </ol>
          </div>
        </section>

        {/* What gets saved: asymmetric three-cell grid */}
        <section className="mx-auto w-full max-w-6xl px-4 py-20 sm:px-6 md:py-28">
          <h2 className="max-w-[20ch] font-display text-3xl font-extrabold tracking-tight text-balance md:text-4xl">
            Small files, big difference
          </h2>
          <div className="mt-10 grid gap-5 md:grid-cols-5">
            <article className="rounded-[var(--radius-card)] bg-accent-soft p-7 md:col-span-3 md:p-9">
              <h3 className="font-display text-2xl font-bold">Your app list, kept for good</h3>
              <p className="mt-3 max-w-[48ch] leading-relaxed text-ink-muted">
                SaveState reads what&apos;s installed, so you only tick boxes. Add a note like which
                version or which settings, and it&apos;s there when you need it.
              </p>
              <ul className="mt-6 flex flex-wrap gap-2" aria-label="Example apps">
                {['Steam', 'Discord', 'VS Code', 'Spotify', 'OBS Studio', '7-Zip'].map((name) => (
                  <li
                    key={name}
                    className="rounded-full border border-line bg-surface px-3 py-1 text-sm font-semibold text-ink"
                  >
                    {name}
                  </li>
                ))}
              </ul>
            </article>

            <article className="rounded-[var(--radius-card)] bg-surface-sunk p-7 md:col-span-2 md:p-9">
              <h3 className="font-display text-2xl font-bold">Configs, packs and mods</h3>
              <p className="mt-3 leading-relaxed text-ink-muted">
                Pick any file or folder. For Minecraft, one click adds the usual suspects.
              </p>
              <ul className="mt-5 grid gap-1.5 font-mono text-[13px] text-ink-muted">
                <li>%APPDATA%\.minecraft\resourcepacks</li>
                <li>%APPDATA%\.minecraft\mods</li>
                <li>%APPDATA%\.minecraft\options.txt</li>
              </ul>
            </article>

            <article className={`${cardClass} flex flex-col gap-5 p-7 sm:flex-row sm:items-center md:col-span-5 md:p-9`}>
              <span className="grid size-14 shrink-0 place-items-center rounded-full bg-accent-soft text-accent">
                <ShieldCheckIcon size={28} weight="duotone" aria-hidden />
              </span>
              <div>
                <h3 className="font-display text-2xl font-bold">Private, and tidy by design</h3>
                <p className="mt-2 max-w-[70ch] leading-relaxed text-ink-muted">
                  Your zip (up to 100 MB) sits in private storage and downloads through links that
                  expire after a minute. Files are removed {EXPIRY_DAYS} days after upload. Your app
                  list stays.
                </p>
              </div>
            </article>
          </div>
        </section>

        {/* Closing call to action */}
        <section className="mx-auto w-full max-w-6xl px-4 pb-8 sm:px-6">
          <div className="flex flex-col items-start gap-6 rounded-[var(--radius-card)] bg-accent px-7 py-10 text-on-accent md:flex-row md:items-center md:justify-between md:px-12">
            <div>
              <h2 className="font-display text-3xl font-extrabold tracking-tight">Reinstalling soon?</h2>
              <p className="mt-2 opacity-85">Back up first. It takes a couple of minutes.</p>
            </div>
            <a
              href={DESKTOP_DOWNLOAD_URL}
              className="inline-flex h-12 items-center gap-2 rounded-[var(--radius-control)] bg-on-accent px-6 font-semibold text-accent transition-transform active:translate-y-px"
            >
              <WindowsLogoIcon size={20} weight="fill" aria-hidden />
              Download for Windows
            </a>
          </div>
        </section>
      </main>

      <SiteFooter />
    </>
  )
}
