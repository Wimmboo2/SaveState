import Link from 'next/link'
import { FloppyDiskBackIcon } from '@phosphor-icons/react/ssr'

export function Logo({ href = '/' }: { href?: string }) {
  return (
    <Link
      href={href}
      className="inline-flex items-center gap-2 rounded-[var(--radius-control)] font-display text-lg font-extrabold tracking-tight text-ink"
    >
      <span className="grid size-8 place-items-center rounded-[10px] bg-accent text-on-accent">
        <FloppyDiskBackIcon size={18} weight="bold" aria-hidden />
      </span>
      SaveState
    </Link>
  )
}
