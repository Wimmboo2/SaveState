import type { Metadata } from 'next'
import { signup } from '../actions'
import { AuthForm } from '../auth-form'

export const metadata: Metadata = { title: 'Create account' }

export default function SignupPage() {
  return (
    <>
      <h1 className="font-display text-3xl font-extrabold tracking-tight">Create your account</h1>
      <p className="mt-2 mb-7 text-ink-muted">Use the same login in the desktop app to back up.</p>
      <AuthForm mode="signup" action={signup} />
    </>
  )
}
