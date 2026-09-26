'use client'

import { useActionState, useEffect, useRef } from 'react'
import { TrashIcon, WarningCircleIcon } from '@phosphor-icons/react'
import { buttonClass } from '@/components/ui'
import { deleteBackupFiles, type DeleteState } from './actions'

/** "Delete backup" with a native <dialog> confirm step. Deletes the zip, keeps the app list. */
export function DeleteBackup({ sizeLabel }: { sizeLabel: string }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const [state, action, pending] = useActionState<DeleteState>(deleteBackupFiles, {})

  useEffect(() => {
    if (state.done) dialog.current?.close()
  }, [state])

  return (
    <>
      <button type="button" onClick={() => dialog.current?.showModal()} className={buttonClass('ghost')}>
        <TrashIcon size={18} aria-hidden />
        Delete backup
      </button>

      <dialog
        ref={dialog}
        aria-labelledby="delete-title"
        aria-describedby="delete-desc"
        className="m-auto w-[min(28rem,calc(100vw-2rem))] rounded-[var(--radius-card)] border border-line bg-surface p-0 text-ink shadow-lift backdrop:bg-ink/40 backdrop:backdrop-blur-[2px]"
      >
        <form action={action} className="p-6 sm:p-7">
          <h2 id="delete-title" className="font-display text-xl font-bold">
            Delete your backup files?
          </h2>
          <p id="delete-desc" className="mt-2 leading-relaxed text-ink-muted">
            Your zip ({sizeLabel}) is deleted right away and can&apos;t be recovered. Your app list and
            notes stay saved.
          </p>
          {state.error && (
            <p role="alert" className="mt-4 flex gap-2.5 rounded-[var(--radius-control)] bg-danger-soft px-3.5 py-3 text-sm">
              <WarningCircleIcon size={20} className="shrink-0 text-danger" aria-hidden />
              {state.error}
            </p>
          )}
          <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
            <button type="button" onClick={() => dialog.current?.close()} className={buttonClass('secondary')} autoFocus>
              Keep backup
            </button>
            <button type="submit" disabled={pending} className={buttonClass('danger')}>
              {pending ? 'Deleting…' : 'Delete files'}
            </button>
          </div>
        </form>
      </dialog>
    </>
  )
}
