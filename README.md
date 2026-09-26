# SaveState

Survive a Windows reinstall without losing your setup.

1. **Back up** — the SaveState desktop app finds your installed apps, lets you tick the ones you want
   to remember (with notes), and zips small files/folders you care about (game configs, Minecraft
   resource packs, mods, settings…). The zip is uploaded to your account.
2. **Reinstall** Windows.
3. **Download** — log in on the website (or the app), see your saved app list, and download your zip.
   A `README.txt` inside tells you where every file goes back.

Files are kept for **30 days** after upload (max **100 MB**). Your app list is kept forever.

## Repo layout

```
desktop/     C# / .NET 10 WinForms app
web/         Next.js website (Vercel)
supabase/
  migrations/  SQL migrations
  functions/   Edge functions (backup, cleanup-expired)
```

## Stack

- **Supabase** — Auth, Postgres (`backups` table with RLS), Edge Functions, pg_cron
- **Cloudflare R2** — private bucket for backup zips, only reachable through short-lived presigned
  URLs handed out by the `backup` edge function
- **Next.js** (App Router) + Tailwind on **Vercel**
- **.NET 10 WinForms** desktop app

Setup and build instructions are coming as the project is built out.
