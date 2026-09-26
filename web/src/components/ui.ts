// Shared class recipes so buttons/inputs/cards look identical everywhere.
// Shape rule: controls = rounded-[var(--radius-control)], surfaces = rounded-[var(--radius-card)].

type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger'
type ButtonSize = 'md' | 'lg'

const base =
  'inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-[var(--radius-control)] font-semibold ' +
  'transition-[background-color,border-color,color,transform] duration-200 ease-out active:translate-y-px ' +
  'disabled:pointer-events-none disabled:opacity-55'

const variants: Record<ButtonVariant, string> = {
  primary: 'bg-accent text-on-accent shadow-soft hover:bg-accent-hover',
  secondary: 'border border-line-strong bg-surface text-ink hover:border-ink-muted',
  ghost: 'text-ink-muted hover:bg-surface-sunk hover:text-ink',
  danger: 'bg-danger text-surface hover:opacity-90',
}

const sizes: Record<ButtonSize, string> = {
  md: 'h-10 px-4 text-sm',
  lg: 'h-12 px-6 text-base',
}

export function buttonClass(variant: ButtonVariant = 'primary', size: ButtonSize = 'md', extra = '') {
  return `${base} ${variants[variant]} ${sizes[size]} ${extra}`.trim()
}

export const inputClass =
  'h-11 w-full rounded-[var(--radius-control)] border border-line-strong bg-surface px-3.5 text-base text-ink ' +
  'placeholder:text-ink-subtle transition-colors hover:border-ink-muted ' +
  'focus:border-accent focus:outline-none focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-accent ' +
  'aria-[invalid=true]:border-danger'

export const cardClass = 'rounded-[var(--radius-card)] border border-line bg-surface shadow-soft'
