import 'server-only'
import { FunctionsHttpError } from '@supabase/supabase-js'
import { createClient } from '@/lib/supabase/server'

type BackupAction = 'download-url' | 'delete' | 'file-list'

export type BackupFunctionResult<T> = { ok: true; data: T } | { ok: false; code: string; message: string }

/**
 * Calls the `backup` edge function as the logged-in user (the Supabase client forwards their JWT).
 * Returns the function's friendly error sentence on failure.
 */
export async function callBackupFunction<T>(action: BackupAction): Promise<BackupFunctionResult<T>> {
  const supabase = await createClient()
  const { data, error } = await supabase.functions.invoke<T>('backup', { body: { action } })
  if (!error && data) return { ok: true, data }

  if (error instanceof FunctionsHttpError) {
    try {
      const body = (await error.context.json()) as { code?: string; error?: string }
      return { ok: false, code: body.code ?? 'server_error', message: body.error ?? 'Something went wrong. Please try again.' }
    } catch {
      // fall through
    }
  }
  return { ok: false, code: 'network', message: "Couldn't reach SaveState's storage. Please try again in a moment." }
}
