import type { Metadata } from 'next'
import { login } from '../actions'
import { AuthForm } from '../auth-form'

export const metadata: Metadata = { title: 'Log in' }

export default function LoginPage() {
  return (
    <>
      <h1 className="font-display text-3xl font-extrabold tracking-tight">Welcome back</h1>
      <p className="mt-2 mb-7 text-ink-muted">Log in to see your apps and download your backup.</p>
      <AuthForm mode="login" action={login} />
    </>
  )
}
