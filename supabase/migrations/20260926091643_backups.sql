-- SaveState: one backup slot per user.
-- The app list (`apps`) is kept forever; the zip lives in Cloudflare R2 and expires after 30 days.
-- Clients may only write `apps`. The file columns are written exclusively by the `backup`
-- and `cleanup-expired` edge functions (service role) after checking the real object in R2,
-- so users can't fake sizes or push their expiry date out.

create table public.backups (
  id          uuid primary key default gen_random_uuid(),
  user_id     uuid not null unique default auth.uid()
                references auth.users (id) on delete cascade,
  apps        jsonb not null default '[]'::jsonb,
  file_path   text,
  size_bytes  bigint not null default 0,
  uploaded_at timestamptz,
  expires_at  timestamptz,
  updated_at  timestamptz not null default now(),

  constraint backups_apps_is_array check (jsonb_typeof(apps) = 'array'),
  -- ~1 MB is thousands of apps with notes; stops anyone using the row as free storage.
  constraint backups_apps_size check (octet_length(apps::text) <= 1048576),
  constraint backups_size_non_negative check (size_bytes >= 0)
);

comment on table public.backups is 'One row per user: saved app list + pointer to their backup zip in R2.';
comment on column public.backups.apps is 'Array of { name, publisher, version, note }. Kept forever.';
comment on column public.backups.file_path is 'Object key in R2 ({user_id}/backup.zip). Null when no files or after expiry.';

-- Keep updated_at fresh on every write.
create function public.set_updated_at()
returns trigger
language plpgsql
set search_path = ''
as $$
begin
  new.updated_at := now();
  return new;
end;
$$;

create trigger backups_set_updated_at
  before update on public.backups
  for each row execute function public.set_updated_at();

-- Row Level Security: users only ever see/touch their own row.
alter table public.backups enable row level security;

create policy "Users can read their own backup"
  on public.backups for select to authenticated
  using ((select auth.uid()) = user_id);

create policy "Users can create their own backup"
  on public.backups for insert to authenticated
  with check ((select auth.uid()) = user_id);

create policy "Users can update their own backup"
  on public.backups for update to authenticated
  using ((select auth.uid()) = user_id)
  with check ((select auth.uid()) = user_id);

create policy "Users can delete their own backup"
  on public.backups for delete to authenticated
  using ((select auth.uid()) = user_id);

-- Column-level privileges: clients can only write `apps` (user_id comes from the default).
revoke all on public.backups from anon, authenticated;
grant select, delete on public.backups to authenticated;
grant insert (apps), update (apps) on public.backups to authenticated;

-- Tiny no-op RPC hit daily by the GitHub keep-alive workflow so the free project isn't paused.
create function public.keepalive()
returns timestamptz
language sql
stable
set search_path = ''
as $$
  select now();
$$;

revoke execute on function public.keepalive() from public;
grant execute on function public.keepalive() to anon, authenticated;

revoke execute on function public.set_updated_at() from public, anon, authenticated;
