'use server'

import { revalidatePath } from 'next/cache'
import { redirect } from 'next/navigation'
import { createClient } from '@/lib/supabase/server'

export type AuthState = {
  error?: string
  fieldErrors?: { email?: string; password?: string }
  notice?: string
  email?: string
}

const MIN_PASSWORD = 8

function readCredentials(formData: FormData) {
  const email = String(formData.get('email') ?? '').trim()
  const password = String(formData.get('password') ?? '')
  const fieldErrors: AuthState['fieldErrors'] = {}
  if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) fieldErrors.email = 'Enter a valid email address.'
  if (!password) fieldErrors.password = 'Enter your password.'
  return { email, password, fieldErrors }
}

/** Turn Supabase Auth errors into plain sentences. */
function friendly(message: string, code?: string): string {
  const m = `${code ?? ''} ${message}`.toLowerCase()
  if (m.includes('invalid_credentials') || m.includes('invalid login credentials'))
    return "That email and password don't match. Try again."
  if (m.includes('user_already_exists') || m.includes('already registered'))
    return 'An account with this email already exists. Log in instead.'
  if (m.includes('email_not_confirmed')) return 'Please confirm your email first, then log in.'
  if (m.includes('weak_password')) return 'Pick a stronger password: at least 8 characters, not a common one.'
  if (m.includes('rate') || m.includes('too many')) return 'Too many attempts. Wait a minute and try again.'
  if (m.includes('fetch') || m.includes('network')) return "Can't reach the server. Check your connection and try again."
  return 'Something went wrong. Please try again.'
}

export async function login(_prev: AuthState, formData: FormData): Promise<AuthState> {
  const { email, password, fieldErrors } = readCredentials(formData)
  if (fieldErrors.email || fieldErrors.password) return { fieldErrors, email }

  const supabase = await createClient()
  const { error } = await supabase.auth.signInWithPassword({ email, password })
  if (error) return { error: friendly(error.message, error.code), email }

  revalidatePath('/', 'layout')
  redirect('/dashboard')
}

export async function signup(_prev: AuthState, formData: FormData): Promise<AuthState> {
  const { email, password, fieldErrors } = readCredentials(formData)
  if (!fieldErrors.password && password.length < MIN_PASSWORD)
    fieldErrors.password = `Use at least ${MIN_PASSWORD} characters.`
  if (fieldErrors.email || fieldErrors.password) return { fieldErrors, email }

  const supabase = await createClient()
  const { data, error } = await supabase.auth.signUp({ email, password })
  if (error) return { error: friendly(error.message, error.code), email }

  // With email confirmation turned off Supabase returns a session straight away.
  if (!data.session) {
    return { notice: 'Check your inbox to confirm your email, then log in.', email }
  }

  revalidatePath('/', 'layout')
  redirect('/dashboard')
}
