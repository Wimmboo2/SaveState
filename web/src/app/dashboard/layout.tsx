import { SignOutIcon } from '@phosphor-icons/react/ssr'
import { redirect } from 'next/navigation'
import { Logo } from '@/components/logo'
import { buttonClass } from '@/components/ui'
import { createClient } from '@/lib/supabase/server'

export default async function DashboardLayout({ children }: { children: React.ReactNode }) {
  // The proxy already redirects logged-out visitors, but never trust that alone.
  const supabase = await createClient()
  const { data } = await supabase.auth.getClaims()
  const claims = data?.claims
  if (!claims?.sub) redirect('/login')

  return (
    <div className="flex min-h-[100dvh] flex-col">
      <header className="border-b border-line bg-surface">
        <div className="mx-auto flex h-16 w-full max-w-5xl items-center justify-between gap-4 px-4 sm:px-6">
          <Logo href="/dashboard" />
          <div className="flex min-w-0 items-center gap-3">
            <span className="hidden truncate text-sm text-ink-muted sm:block">{claims.email as string}</span>
            <form action="/auth/signout" method="post">
              <button type="submit" className={buttonClass('ghost')}>
                <SignOutIcon size={18} aria-hidden />
                Log out
              </button>
            </form>
          </div>
        </div>
      </header>
      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-10 sm:px-6 md:py-14">{children}</main>
    </div>
  )
}
