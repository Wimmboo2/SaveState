'use client'

import Link from 'next/link'
import { useActionState } from 'react'
import { WarningCircleIcon, CheckCircleIcon } from '@phosphor-icons/react'
import { buttonClass, inputClass } from '@/components/ui'
import type { AuthState } from './actions'

type Props = {
  mode: 'login' | 'signup'
  action: (state: AuthState, formData: FormData) => Promise<AuthState>
}

export function AuthForm({ mode, action }: Props) {
  const [state, formAction, pending] = useActionState(action, {})
  const isSignup = mode === 'signup'

  return (
    <form action={formAction} noValidate className="grid gap-5">
      {state.error && (
        <p role="alert" className="flex gap-2.5 rounded-[var(--radius-control)] bg-danger-soft px-3.5 py-3 text-sm text-ink">
          <WarningCircleIcon size={20} className="shrink-0 text-danger" aria-hidden />
          {state.error}
        </p>
      )}
      {state.notice && (
        <p role="status" className="flex gap-2.5 rounded-[var(--radius-control)] bg-accent-soft px-3.5 py-3 text-sm text-ink">
          <CheckCircleIcon size={20} className="shrink-0 text-accent" aria-hidden />
          {state.notice}
        </p>
      )}

      <div className="grid gap-2">
        <label htmlFor="email" className="text-sm font-semibold">
          Email
        </label>
        <input
          id="email"
          name="email"
          type="email"
          autoComplete="email"
          required
          defaultValue={state.email}
          aria-invalid={Boolean(state.fieldErrors?.email)}
          aria-describedby={state.fieldErrors?.email ? 'email-error' : undefined}
          className={inputClass}
        />
        {state.fieldErrors?.email && (
          <p id="email-error" className="text-sm text-danger">
            {state.fieldErrors.email}
          </p>
        )}
      </div>

      <div className="grid gap-2">
        <label htmlFor="password" className="text-sm font-semibold">
          Password
        </label>
        <input
          id="password"
          name="password"
          type="password"
          autoComplete={isSignup ? 'new-password' : 'current-password'}
          required
          minLength={isSignup ? 8 : undefined}
          aria-invalid={Boolean(state.fieldErrors?.password)}
          aria-describedby={
            state.fieldErrors?.password ? 'password-error' : isSignup ? 'password-help' : undefined
          }
          className={inputClass}
        />
        {state.fieldErrors?.password ? (
          <p id="password-error" className="text-sm text-danger">
            {state.fieldErrors.password}
          </p>
        ) : (
          isSignup && (
            <p id="password-help" className="text-sm text-ink-muted">
              At least 8 characters.
            </p>
          )
        )}
      </div>

      <button type="submit" disabled={pending} className={buttonClass('primary', 'lg', 'mt-1 w-full')}>
        {pending ? (isSignup ? 'Creating account…' : 'Logging in…') : isSignup ? 'Create account' : 'Log in'}
      </button>

      <p className="text-center text-sm text-ink-muted">
        {isSignup ? 'Already have an account? ' : 'New to SaveState? '}
        <Link
          href={isSignup ? '/login' : '/signup'}
          className="font-semibold text-accent underline-offset-4 hover:underline"
        >
          {isSignup ? 'Log in' : 'Create account'}
        </Link>
      </p>
    </form>
  )
}
