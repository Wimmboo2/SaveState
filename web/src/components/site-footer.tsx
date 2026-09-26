import { GithubLogoIcon } from '@phosphor-icons/react/ssr'
import { GITHUB_URL } from '@/lib/constants'

export function SiteFooter() {
  return (
    <footer className="mx-auto flex w-full max-w-6xl flex-col gap-3 px-4 py-10 text-sm text-ink-muted sm:flex-row sm:items-center sm:justify-between sm:px-6">
      <p>SaveState keeps your setup through a Windows reinstall.</p>
      <a
        href={GITHUB_URL}
        className="inline-flex items-center gap-1.5 rounded-[var(--radius-control)] hover:text-ink"
      >
        <GithubLogoIcon size={16} aria-hidden />
        Source on GitHub
      </a>
    </footer>
  )
}
