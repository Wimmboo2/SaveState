'use server'

import { revalidatePath } from 'next/cache'
import { callBackupFunction } from '@/lib/backup-function'

export type DeleteState = { error?: string; done?: boolean }

/** Deletes the stored zip. The app list stays. */
export async function deleteBackupFiles(): Promise<DeleteState> {
  const result = await callBackupFunction('delete')
  if (!result.ok) return { error: result.message }
  revalidatePath('/dashboard')
  return { done: true }
}
