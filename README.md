# SaveState

Survive a Windows reinstall without losing your setup.

1. **Back up**: the SaveState desktop app finds your installed apps, lets you tick the ones you
   want to remember (with notes), and zips the small files that make your PC yours: game
   configs, Minecraft resource packs, mods, settings. The zip goes to your account.
2. **Reinstall** Windows.
3. **Download**: log in on the website (or in the app), check your app list and download your zip.
   `README.txt` inside it says where every file goes back.

Files are kept for **30 days** after each upload (max **100 MB**). Your app list is kept forever.

- Website: https://savestate-woad.vercel.app
- Desktop app: [latest release](https://github.com/Wimmboo2/SaveState/releases/latest)

## How it fits together

```
 Desktop app (WinForms, .NET 10)        Website (Next.js on Vercel)
   │  login, app list (PostgREST)          │  login, app list, download/delete
   │                                        │
   ▼                                        ▼
 Supabase ─ Auth (email + password)
          ─ Postgres: public.backups (one row per user, RLS)
          ─ Edge Function `backup` ──────► Cloudflare R2 (private bucket, presigned URLs)
          ─ Edge Function `cleanup-expired` ◄── pg_cron, daily
```

- **One row per user** in `public.backups`: `apps` (jsonb, kept forever) plus file metadata.
  Row Level Security limits every user to their own row. Clients may only write `apps`;
  `file_path`, `size_bytes`, `uploaded_at`, `expires_at` are written only by the edge functions
  after checking the real object in storage, so nobody can fake an expiry date.
- **Files live in Cloudflare R2**, not Supabase Storage. Supabase's free plan caps files at 50 MB
  and 1 GB in total; R2's free tier is 10 GB with free downloads. The bucket is private. The
  `backup` edge function checks the caller's login and hands out short-lived presigned URLs for
  exactly one object, `{user_id}/backup.zip` (upload link: 10 minutes, bound to the exact size;
  download link: 60 seconds). The R2 keys exist only in Edge Function secrets.
- **Cleanup**: `cleanup-expired` runs daily at 03:17 UTC (pg_cron + pg_net). It deletes expired zips
  through the R2 API, clears the row's file columns (the app list stays), and sweeps anything
  orphaned or oversized. It only runs with an `x-cron-secret` header matching a secret kept in
  Supabase Vault.
- **Keep-alive**: free Supabase projects pause after ~7 idle days. `.github/workflows/keepalive.yml`
  runs one tiny real query every day (it only runs from the default branch, `main`).

### Repo layout

```
desktop/                  .NET 10 solution
  SaveState.Core/         API client, app filtering, zip builder, presets (cross-platform)
  SaveState/              WinForms UI (Windows only)
  SaveState.Core.Tests/   xUnit tests (run anywhere)
web/                      Next.js 16 (App Router) + Tailwind v4 + @supabase/ssr
supabase/
  migrations/             SQL: backups table + RLS, cleanup cron
  functions/              Edge functions: backup, cleanup-expired (+ _shared)
.github/workflows/        desktop CI + release, Supabase keep-alive
```

## Security model

- Bucket is private; downloads only through 60-second presigned URLs.
- RLS on `backups`; column grants so clients can't touch file metadata.
- 100 MB limit enforced in three places: the desktop app, the `upload-url` request (and the
  presigned URL's signed content length), and a HEAD check when the upload is confirmed.
- The service role key and the R2 keys exist **only** in the Edge Function environment. The desktop
  app and website only carry the Supabase URL + publishable key, which are public by design
  (they're in every client and protected by RLS).
- `.env*` files are gitignored; see `web/.env.example` and `supabase/functions/.env.example`.
- The cleanup function rejects any call without the Vault-backed secret.

## Setting it up from scratch

You need free accounts on Supabase, Cloudflare (R2 needs a card on file, but you're only charged
beyond the free tier) and Vercel.

### 1. Supabase

1. Create a project. Apply the migrations in `supabase/migrations/` in order (SQL editor or
   `supabase db push`).
2. Create the two Vault secrets used by the cron job (SQL editor):
   ```sql
   select vault.create_secret('https://<project-ref>.supabase.co', 'project_url');
   select vault.create_secret(encode(extensions.gen_random_bytes(32), 'hex'), 'cleanup_cron_secret');
   ```
3. Deploy the edge functions with **JWT verification off** (both check auth themselves):
   `supabase functions deploy backup --no-verify-jwt` and
   `supabase functions deploy cleanup-expired --no-verify-jwt`.
4. **Authentication → Sign In / Providers → Email**: turn off **Confirm email** (Supabase's
   built-in mailer only delivers to your own team; add custom SMTP later if you want verification).
5. **Authentication → URL Configuration**: Site URL = your website URL; add redirect URLs
   `<site>/**` and `http://localhost:3000/**`.

### 2. Cloudflare R2

1. Enable R2, create a bucket (e.g. `savestate-backups`), keep it private (no public access, no
   custom domain).
2. Add a lifecycle rule: delete objects 31 days after upload (safety net if the cron ever stops).
3. Create an R2 API token with **Object Read & Write** for that bucket only.
4. In **Supabase → Edge Functions → Secrets**, add `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`,
   `R2_SECRET_ACCESS_KEY`, `R2_BUCKET`.

### 3. Website (Vercel)

1. Import the repo, set the root directory to `web`.
2. Environment variables: `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_ANON_KEY` (the
   publishable key).

Local development:

```bash
cd web
cp .env.example .env.local   # fill in URL + publishable key
npm install
npm run dev                  # http://localhost:3000
npm run lint && npx tsc --noEmit && npm run build
```

### 4. Desktop app

The Supabase URL + publishable key are in `desktop/SaveState.Core/Config/AppConfig.cs`.

```bash
cd desktop
dotnet build SaveState.sln          # builds on any OS (EnableWindowsTargeting); runs on Windows
dotnet test SaveState.sln           # unit tests
dotnet run --project SaveState      # Windows only

# Self-contained single-file exe (what the release ships)
dotnet publish SaveState/SaveState.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish
```

Optional live tests against a real project (use a throwaway account):
`SAVESTATE_TEST_EMAIL=… SAVESTATE_TEST_PASSWORD=… [SAVESTATE_TEST_STORAGE=1] dotnet test`.

`SaveState.exe --screenshots <dir>` renders every screen with sample data (used by CI).

**Releasing:** Actions → *Desktop* → *Run workflow* with a version (e.g. `1.0.1`). CI builds,
tests and attaches `SaveState.exe` to a new GitHub release `v1.0.1`.

## Restoring after a reinstall

1. Log in on the website (or in the desktop app) and download your backup.
2. Unzip it and open `README.txt`.
3. Close the app the files belong to, then copy each `files\...` folder back to the location
   listed next to it. Paste locations like `%APPDATA%` into File Explorer's address bar.
4. Reinstall your apps using the saved list and notes.

## Where things are stored locally

`%LOCALAPPDATA%\SaveState\`: `session.dat` (login, encrypted with Windows DPAPI for your
Windows account only), `selection.json` (picked paths), `log.txt` (no tokens or file contents).

## Known limits

- The exe isn't code-signed yet, so Windows SmartScreen warns on first run ("More info" → "Run
  anyway").
- Free Supabase projects pause after about a week without activity; the keep-alive workflow
  prevents that once this branch is merged into `main`. GitHub disables scheduled workflows in
  public repos after 60 days without commits (it emails you; re-enabling is one click).
- Leaked-password protection (HaveIBeenPwned check) is a Supabase Pro feature and is off.
- Backups are one zip per account, up to 100 MB; a new backup replaces the old one.
