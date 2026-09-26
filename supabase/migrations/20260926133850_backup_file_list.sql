-- What's inside the stored zip, and what happened to it when it's gone.
-- Written only by the edge functions (service role): clients still may only write `apps`.

alter table public.backups
  add column files jsonb,
  add column files_removed_at timestamptz,
  add column files_removed_reason text,
  add constraint backups_files_is_object check (files is null or jsonb_typeof(files) = 'object'),
  -- The list is capped at 5,000 files by the edge function; this is a backstop.
  add constraint backups_files_size check (files is null or octet_length(files::text) <= 2097152),
  add constraint backups_files_removed_reason check (files_removed_reason in ('deleted', 'expired'));

comment on column public.backups.files is
  'Summary of the zip read from its manifest.json by the backup edge function: { file_count, total_bytes, listed_count, items: [{ path, kind, file_count, size_bytes, files: [{ path, size_bytes }] }] } or { unavailable: true }. Null when there are no files.';
comment on column public.backups.files_removed_at is 'When the last stored zip was removed (deleted by the user or expired). Cleared by a new upload.';
comment on column public.backups.files_removed_reason is '''deleted'' (the user deleted it) or ''expired'' (30 days passed).';
