-- Daily cleanup of expired backup zips.
-- pg_cron calls the `cleanup-expired` edge function once a day via pg_net. The request carries a
-- secret header (x-cron-secret). Both the project URL and the secret live in Supabase Vault
-- (created separately with vault.create_secret, never committed to git):
--
--   select vault.create_secret('https://<project-ref>.supabase.co', 'project_url');
--   select vault.create_secret(encode(extensions.gen_random_bytes(32), 'hex'), 'cleanup_cron_secret');

create extension if not exists pg_cron;
create extension if not exists pg_net with schema extensions;

-- Lets the edge function check the header against the Vault secret without ever reading it out.
-- Only the service role (used inside the edge function) may call it.
create function public.verify_cleanup_secret(candidate text)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select exists (
    select 1
    from vault.decrypted_secrets
    where name = 'cleanup_cron_secret'
      and decrypted_secret = candidate
  );
$$;

revoke execute on function public.verify_cleanup_secret(text) from public, anon, authenticated;
grant execute on function public.verify_cleanup_secret(text) to service_role;

-- Every day at 03:17 UTC.
select cron.schedule(
  'cleanup-expired-backups',
  '17 3 * * *',
  $$
  select net.http_post(
    url := (select decrypted_secret from vault.decrypted_secrets where name = 'project_url') || '/functions/v1/cleanup-expired',
    headers := jsonb_build_object(
      'Content-Type', 'application/json',
      'x-cron-secret', (select decrypted_secret from vault.decrypted_secrets where name = 'cleanup_cron_secret')
    ),
    body := '{}'::jsonb,
    timeout_milliseconds := 60000
  );
  $$
);
