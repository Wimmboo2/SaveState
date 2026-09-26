import Link from 'next/link'
import { Logo } from './logo'
import { buttonClass } from './ui'

export function SiteHeader({ isLoggedIn }: { isLoggedIn: boolean }) {
  return (
    <header className="mx-auto flex h-16 w-full max-w-6xl items-center justify-between gap-4 px-4 sm:px-6">
      <Logo />
      <nav aria-label="Account" className="flex items-center gap-2">
        {isLoggedIn ? (
          <Link href="/dashboard" className={buttonClass('primary')}>
            Open dashboard
          </Link>
        ) : (
          <>
            <Link href="/login" className={buttonClass('ghost')}>
              Log in
            </Link>
            {/* On phones the hero already shows this button, so keep the header light. */}
            <span className="hidden sm:contents">
              <Link href="/signup" className={buttonClass('secondary')}>
                Create account
              </Link>
            </span>
          </>
        )}
      </nav>
    </header>
  )
}
