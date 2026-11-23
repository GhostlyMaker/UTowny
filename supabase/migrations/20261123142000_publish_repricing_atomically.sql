-- Publish bulk historical repricing atomically after bounded batches complete.
set local lock_timeout='5s';
set local statement_timeout='60s';
create table private.economy_history_publish_state (
 singleton boolean primary key default true check(singleton),
 repricing boolean not null default false
);
insert into private.economy_history_publish_state(singleton) values(true);
alter table private.economy_history_publish_state enable row level security;
revoke all on private.economy_history_publish_state from public,anon,authenticated;
create table private.economy_wealth_history_staging as table private.economy_group_wealth_history_cache with no data;
create unique index economy_wealth_staging_pk on private.economy_wealth_history_staging(snapshot_id,group_id);
alter table private.economy_wealth_history_staging enable row level security;
revoke all on private.economy_wealth_history_staging from public,anon,authenticated;

create or replace function private.refresh_economy_wealth_on_price_change()
returns trigger language plpgsql security definer set search_path='' as $prices$
begin
 perform pg_advisory_xact_lock(hashtext('economy-history-refresh'));
 refresh materialized view public.economy_current_group_wealth;
 refresh materialized view public.economy_current_group_wealth_breakdown;
 refresh materialized view public.economy_public_price_guide;
 delete from private.economy_wealth_history_staging;
 insert into private.economy_wealth_history_staging select * from private.economy_group_wealth_history_cache;
 update private.economy_history_publish_state set repricing=true where singleton;
 insert into private.economy_history_queue(snapshot_id)
 select id from public.economy_snapshots
 on conflict(snapshot_id) do update set queued_at=clock_timestamp();
 perform private.mark_economy_history_dirty();
 return null;
end $prices$;

create or replace function private.process_economy_history_queue(p_limit integer default 10)
returns integer language plpgsql security definer set search_path='' as $worker$
declare v_id uuid; v_processed integer:=0; v_started timestamptz:=clock_timestamp(); v_repricing boolean;
begin
 if not pg_try_advisory_xact_lock(hashtext('economy-history-refresh')) then return 0; end if;
 select repricing into v_repricing from private.economy_history_publish_state where singleton;
 for v_id in select snapshot_id from private.economy_history_queue order by queued_at,snapshot_id
   limit greatest(1,least(coalesce(p_limit,10),25)) for update skip locked
 loop
  if v_repricing then
   delete from private.economy_wealth_history_staging where snapshot_id=v_id;
   insert into private.economy_wealth_history_staging select * from private.calculate_economy_group_wealth_history(v_id);
  else
   delete from private.economy_group_wealth_history_cache where snapshot_id=v_id;
   insert into private.economy_group_wealth_history_cache select * from private.calculate_economy_group_wealth_history(v_id);
  end if;
  delete from private.economy_category_history_cache where snapshot_id=v_id;
  insert into private.economy_category_history_cache select * from private.calculate_economy_category_history(v_id);
  delete from private.economy_group_category_history_cache where snapshot_id=v_id;
  insert into private.economy_group_category_history_cache select * from private.calculate_economy_group_category_history(v_id);
  delete from private.economy_history_queue where snapshot_id=v_id;
  v_processed:=v_processed+1;
  exit when clock_timestamp()-v_started>interval '10 seconds';
 end loop;
 if v_repricing and not exists(select 1 from private.economy_history_queue) then
  delete from private.economy_group_wealth_history_cache;
  insert into private.economy_group_wealth_history_cache select * from private.economy_wealth_history_staging;
  delete from private.economy_wealth_history_staging;
  update private.economy_history_publish_state set repricing=false where singleton;
 end if;
 return v_processed;
end $worker$;

