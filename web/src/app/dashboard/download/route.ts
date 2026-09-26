import { NextResponse, type NextRequest } from 'next/server'
import { callBackupFunction } from '@/lib/backup-function'

/**
 * GET /dashboard/download → asks the backup function for a 60-second signed link and redirects
 * the browser straight to it (the file downloads as an attachment). Errors bounce back to the
 * dashboard with a code it knows how to explain.
 */
export async function GET(request: NextRequest) {
  const result = await callBackupFunction<{ url: string }>('download-url')
  if (result.ok) return NextResponse.redirect(result.data.url, { status: 303 })

  const back = new URL('/dashboard', request.url)
  back.searchParams.set('error', result.code)
  return NextResponse.redirect(back, { status: 303 })
}
