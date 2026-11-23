-- Switch the public read interfaces only after validating staged calculations.
set local lock_timeout='5s';
set local statement_timeout='90s';
set local search_path='';

create function private.queue_economy_dimension_history() returns trigger
language plpgsql security definer set search_path='' as $dimension$
begin
 if TG_TABLE_NAME='economy_items' then
  if TG_OP='UPDATE' then
   insert into private.economy_history_queue(snapshot_id)
    select distinct si.snapshot_id from public.economy_snapshot_items si
    join (select n.item_key from new_dimensions n join old_dimensions o using(item_key)
          where n.item_type is distinct from o.item_type) changed using(item_key)
   on conflict(snapshot_id) do update set queued_at=clock_timestamp();
  elsif TG_OP='INSERT' then
   insert into private.economy_history_queue(snapshot_id)
    select distinct si.snapshot_id from public.economy_snapshot_items si join new_dimensions n using(item_key)
   on conflict(snapshot_id) do update set queued_at=clock_timestamp();
  else
   insert into private.economy_history_queue(snapshot_id)
    select distinct si.snapshot_id from public.economy_snapshot_items si join old_dimensions o using(item_key)
   on conflict(snapshot_id) do update set queued_at=clock_timestamp();
  end if;
 else
  if TG_OP='UPDATE' then
   insert into private.economy_history_queue(snapshot_id)
    select distinct si.snapshot_id from public.economy_snapshot_items si
    join (select n.server_id,n.group_id from new_dimensions n join old_dimensions o using(server_id,group_id)
          where n.name is distinct from o.name) changed using(server_id,group_id)
   on conflict(snapshot_id) do update set queued_at=clock_timestamp();
  elsif TG_OP='INSERT' then
   insert into private.economy_history_queue(snapshot_id)
    select distinct si.snapshot_id from public.economy_snapshot_items si join new_dimensions n using(server_id,group_id)
   on conflict(snapshot_id) do update set queued_at=clock_timestamp();
  else
   insert into private.economy_history_queue(snapshot_id)
    select distinct si.snapshot_id from public.economy_snapshot_items si join old_dimensions o using(server_id,group_id)
   on conflict(snapshot_id) do update set queued_at=clock_timestamp();
  end if;
 end if;
 return null;
end $dimension$;
revoke all on function private.queue_economy_dimension_history() from public,anon,authenticated;

do $triggers$
declare t text;
begin
 foreach t in array array['economy_items','economy_groups'] loop
  execute format('create trigger economy_queue_dimension_insert after insert on public.%I referencing new table as new_dimensions for each statement execute function private.queue_economy_dimension_history()',t);
  execute format('create trigger economy_queue_dimension_update after update on public.%I referencing old table as old_dimensions new table as new_dimensions for each statement execute function private.queue_economy_dimension_history()',t);
  execute format('create trigger economy_queue_dimension_delete after delete on public.%I referencing old table as old_dimensions for each statement execute function private.queue_economy_dimension_history()',t);
 end loop;
end $triggers$;

create or replace function private.refresh_economy_wealth_on_price_change()
returns trigger language plpgsql security definer set search_path='' as $prices$
begin
 refresh materialized view public.economy_current_group_wealth;
 refresh materialized view public.economy_current_group_wealth_breakdown;
 refresh materialized view public.economy_public_price_guide;
 -- History still uses the latest prices for ALL snapshots, exactly as before.
 insert into private.economy_history_queue(snapshot_id)
 select id from public.economy_snapshots
 on conflict(snapshot_id) do update set queued_at=clock_timestamp();
 perform private.mark_economy_history_dirty();
 return null;
end $prices$;

create or replace function private.refresh_economy_history_if_dirty()
returns void language plpgsql security definer set search_path='' as $refresh$
declare n integer;
begin
 n:=private.process_economy_history_queue(25);
 update private.economy_refresh_state
 set history_dirty=exists(select 1 from private.economy_history_queue),
     history_refreshed_at=case when n>0 then clock_timestamp() else history_refreshed_at end
 where singleton=true;
end $refresh$;

-- Catch any snapshot replacement since the last old-style successful refresh.
insert into private.economy_history_queue(snapshot_id)
select s.id from public.economy_snapshots s
where s.received_at >= coalesce((select history_refreshed_at from private.economy_refresh_state where singleton),'-infinity'::timestamptz)
on conflict(snapshot_id) do nothing;

do $switch$
declare n text;
begin
 foreach n in array array['economy_group_wealth_history','economy_category_history','economy_group_category_history'] loop
  execute format('alter materialized view public.%I rename to %I',n,n||'_legacy_20261123');
  execute format('alter materialized view public.%I set schema private',n||'_legacy_20261123');
  execute format('revoke all on private.%I from public,anon,authenticated',n||'_legacy_20261123');
  execute format('create view public.%I as select * from private.%I',n,n||'_cache');
  execute format('revoke all on public.%I from public,anon,authenticated',n);
  execute format('grant select on public.%I to anon,authenticated,service_role',n);
 end loop;
end $switch$;

analyze private.economy_group_wealth_history_cache;
analyze private.economy_category_history_cache;
analyze private.economy_group_category_history_cache;

-- Short bounded batches replace the former unbounded full-history rebuild.
select cron.alter_job(jobid, schedule:='* * * * *',command:='set statement_timeout = ''25s''; select private.refresh_economy_history_if_dirty();',active:=true)
from cron.job where jobid=1 and command='select private.refresh_economy_history_if_dirty();';
notify pgrst, 'reload schema';

