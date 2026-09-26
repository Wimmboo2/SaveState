import { createClient, type SupabaseClient } from 'npm:@supabase/supabase-js@2.117.2'

/**
 * Service-role client. The key is injected by Supabase into the Edge Function environment and
 * never leaves the server. It bypasses RLS, so only use it after checking who the caller is.
 */
export function adminClient(): SupabaseClient {
  const url = Deno.env.get('SUPABASE_URL')!
  const key = Deno.env.get('SUPABASE_SERVICE_ROLE_KEY') ?? secretKeyFromDict()
  if (!key) throw new Error('No service role / secret key available in the function environment')
  return createClient(url, key, { auth: { persistSession: false, autoRefreshToken: false } })
}

function secretKeyFromDict(): string | undefined {
  const raw = Deno.env.get('SUPABASE_SECRET_KEYS')
  if (!raw) return undefined
  try {
    return JSON.parse(raw).default
  } catch {
    return undefined
  }
}
