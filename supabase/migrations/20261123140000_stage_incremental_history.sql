-- Stage incremental history without changing public endpoints.
set local lock_timeout = '5s';
set local statement_timeout = '90s';
set local search_path = '';

create table private.economy_history_queue (
 snapshot_id uuid primary key,
 queued_at timestamptz not null default clock_timestamp()
);
alter table private.economy_history_queue enable row level security;
revoke all on private.economy_history_queue from public, anon, authenticated;

do $stage$
declare v_name text; v_sql text;
begin
 foreach v_name in array array['economy_group_wealth_history','economy_category_history','economy_group_category_history'] loop
  execute format('create table private.%I as table public.%I',v_name||'_cache',v_name);
  execute format('alter table private.%I enable row level security',v_name||'_cache');
  execute format('revoke all on private.%I from public, anon, authenticated',v_name||'_cache');
  v_sql := pg_get_viewdef(format('public.%I',v_name)::regclass,true);
  if position('public.economy_snapshots s' in v_sql)=0 then raise exception 'Unexpected history definition: %',v_name; end if;
  v_sql := replace(v_sql,'public.economy_snapshots s','(select * from public.economy_snapshots where id = p_snapshot_id) s');
  execute format('create function private.%I(p_snapshot_id uuid) returns setof private.%I language sql stable set search_path = '''' as %L',
     'calculate_'||v_name,v_name||'_cache',v_sql);
  execute format('revoke all on function private.%I(uuid) from public, anon, authenticated','calculate_'||v_name);
 end loop;
end $stage$;

create unique index economy_wealth_cache_pk on private.economy_group_wealth_history_cache(snapshot_id,group_id);
create index economy_wealth_cache_lookup on private.economy_group_wealth_history_cache(server_id,group_id,generated_at desc);
create unique index economy_category_cache_pk on private.economy_category_history_cache(snapshot_id,category);
create index economy_category_cache_lookup on private.economy_category_history_cache(server_id,snapshot_id,category);
create unique index economy_group_category_cache_pk on private.economy_group_category_history_cache(snapshot_id,group_id,category);
create index economy_group_category_cache_lookup on private.economy_group_category_history_cache(server_id,group_id,snapshot_id,category);

create function private.process_economy_history_queue(p_limit integer default 10)
returns integer language plpgsql security definer set search_path='' as $worker$
declare v_id uuid; v_processed integer:=0; v_started timestamptz:=clock_timestamp();
begin
 if not pg_try_advisory_xact_lock(hashtext('economy-history-refresh')) then return 0; end if;
 for v_id in select snapshot_id from private.economy_history_queue order by queued_at,snapshot_id
   limit greatest(1,least(coalesce(p_limit,10),25)) for update skip locked
 loop
  delete from private.economy_group_wealth_history_cache where snapshot_id=v_id;
  insert into private.economy_group_wealth_history_cache select * from private.calculate_economy_group_wealth_history(v_id);
  delete from private.economy_category_history_cache where snapshot_id=v_id;
  insert into private.economy_category_history_cache select * from private.calculate_economy_category_history(v_id);
  delete from private.economy_group_category_history_cache where snapshot_id=v_id;
  insert into private.economy_group_category_history_cache select * from private.calculate_economy_group_category_history(v_id);
  delete from private.economy_history_queue where snapshot_id=v_id;
  v_processed:=v_processed+1;
  exit when clock_timestamp()-v_started>interval '10 seconds';
 end loop;
 return v_processed;
end $worker$;
revoke all on function private.process_economy_history_queue(integer) from public,anon,authenticated;

-- Capture every source mutation while the existing public history remains readable.
create function private.queue_economy_snapshot_history() returns trigger
language plpgsql security definer set search_path='' as $trigger$
begin
 if TG_OP='DELETE' then
  insert into private.economy_history_queue(snapshot_id) values(old.id)
  on conflict(snapshot_id) do update set queued_at=clock_timestamp();
 else
  insert into private.economy_history_queue(snapshot_id) values(new.id)
  on conflict(snapshot_id) do update set queued_at=clock_timestamp();
 end if;
 return null;
end $trigger$;
revoke all on function private.queue_economy_snapshot_history() from public,anon,authenticated;
create trigger economy_queue_snapshot_history after insert or update or delete on public.economy_snapshots
for each row execute function private.queue_economy_snapshot_history();

create function private.queue_economy_item_history() returns trigger
language plpgsql security definer set search_path='' as $trigger$
begin
 if TG_OP <> 'DELETE' then
  insert into private.economy_history_queue(snapshot_id) select distinct snapshot_id from new_items
  on conflict(snapshot_id) do update set queued_at=clock_timestamp();
 end if;
 if TG_OP <> 'INSERT' then
  insert into private.economy_history_queue(snapshot_id) select distinct snapshot_id from old_items
  on conflict(snapshot_id) do update set queued_at=clock_timestamp();
 end if;
 return null;
end $trigger$;
revoke all on function private.queue_economy_item_history() from public,anon,authenticated;
create trigger economy_queue_item_insert after insert on public.economy_snapshot_items
referencing new table as new_items for each statement execute function private.queue_economy_item_history();
create trigger economy_queue_item_update after update on public.economy_snapshot_items
referencing old table as old_items new table as new_items for each statement execute function private.queue_economy_item_history();
create trigger economy_queue_item_delete after delete on public.economy_snapshot_items
referencing old table as old_items for each statement execute function private.queue_economy_item_history();

-- Reconcile snapshots not yet represented by the old successful refresh.
insert into private.economy_history_queue(snapshot_id)
 select s.id from public.economy_snapshots s where
 not exists(select 1 from private.economy_group_wealth_history_cache h where h.snapshot_id=s.id)
 or not exists(select 1 from private.economy_category_history_cache h where h.snapshot_id=s.id)
 or not exists(select 1 from private.economy_group_category_history_cache h where h.snapshot_id=s.id)
on conflict(snapshot_id) do nothing;

