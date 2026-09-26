import type { Metadata, Viewport } from 'next'
import { Nunito, Nunito_Sans } from 'next/font/google'
import './globals.css'

// Rounded display face for headings, humanist sans for body. Friendly, calm, readable.
const nunito = Nunito({ subsets: ['latin'], variable: '--font-nunito', display: 'swap' })
const nunitoSans = Nunito_Sans({ subsets: ['latin'], variable: '--font-nunito-sans', display: 'swap' })

export const metadata: Metadata = {
  title: {
    default: 'SaveState: keep your setup through a Windows reinstall',
    template: '%s · SaveState',
  },
  description:
    'Remember your installed apps and back up the small files that make your PC yours, then download them after reinstalling Windows.',
}

export const viewport: Viewport = {
  themeColor: [
    { media: '(prefers-color-scheme: light)', color: '#edefe7' },
    { media: '(prefers-color-scheme: dark)', color: '#131915' },
  ],
}

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en" className={`${nunito.variable} ${nunitoSans.variable}`}>
      <body className="min-h-[100dvh] antialiased">{children}</body>
    </html>
  )
}
