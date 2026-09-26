import { Logo } from '@/components/logo'
import { cardClass } from '@/components/ui'

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-[100dvh] flex-col">
      <header className="mx-auto flex h-16 w-full max-w-6xl items-center px-4 sm:px-6">
        <Logo />
      </header>
      <main className="flex flex-1 items-start justify-center px-4 pt-8 pb-16 sm:items-center sm:pt-0">
        <div className={`${cardClass} w-full max-w-[26rem] p-6 sm:p-8`}>{children}</div>
      </main>
    </div>
  )
}
