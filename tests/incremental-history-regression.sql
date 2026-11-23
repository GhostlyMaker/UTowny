-- Regression checks run in subtransactions that always roll back their writes.
do $tests$
declare v_id uuid; v_group text; v_key text; v_old_count bigint; v_before numeric;
begin
 begin
  perform pg_advisory_xact_lock(hashtext('economy-history-refresh'));
  if exists(select 1 from private.economy_history_queue) then raise exception 'Queue must be drained before regression test'; end if;
  select snapshot_id,group_id,item_key into v_id,v_group,v_key from public.economy_snapshot_items limit 1;
  update public.economy_snapshot_items set contained_amount=contained_amount+1
   where snapshot_id=v_id and group_id=v_group and item_key=v_key;
  if not exists(select 1 from private.economy_history_queue where snapshot_id=v_id) then raise exception 'Item update was not queued'; end if;
  perform private.process_economy_history_queue(1);
  if exists(
   (select * from private.calculate_economy_group_category_history(v_id)
    except all select * from public.economy_group_category_history where snapshot_id=v_id)
   union all
   (select * from public.economy_group_category_history where snapshot_id=v_id
    except all select * from private.calculate_economy_group_category_history(v_id))
  ) then raise exception 'Updated snapshot calculation differs'; end if;
  raise exception using errcode='P9999',message='rollback successful item test';
 exception when sqlstate 'P9999' then null; end;
 begin
  perform pg_advisory_xact_lock(hashtext('economy-history-refresh'));
  select g.group_id into v_group from public.economy_groups g where g.server_id='main' and g.group_id<>'0' and g.name is not null and exists(select 1 from public.economy_snapshot_items i where i.server_id=g.server_id and i.group_id=g.group_id) limit 1;
  update public.economy_groups set name=name where server_id='main' and group_id=v_group;
  if exists(select 1 from private.economy_history_queue) then raise exception 'Unchanged group name unnecessarily queued history'; end if;
  update public.economy_groups set name=name||' regression-test' where server_id='main' and group_id=v_group;
  if not exists(select 1 from private.economy_history_queue) then raise exception 'Group rename did not invalidate history'; end if;
  raise exception using errcode='P9999',message='rollback successful group test';
 exception when sqlstate 'P9999' then null; end;
 begin
  perform pg_advisory_xact_lock(hashtext('economy-history-refresh'));
  select count(*),sum(wealth) into v_old_count,v_before from private.economy_group_wealth_history_cache;
  insert into private.economy_wealth_history_staging select * from private.economy_group_wealth_history_cache;
  update private.economy_history_publish_state set repricing=true where singleton;
  insert into private.economy_history_queue(snapshot_id) select id from public.economy_snapshots order by generated_at desc limit 2;
  perform private.process_economy_history_queue(1);
  if not (select repricing from private.economy_history_publish_state where singleton) then raise exception 'Repricing published before queue completed'; end if;
  if (select sum(wealth) from public.economy_group_wealth_history) is distinct from v_before then raise exception 'Partial repricing leaked'; end if;
  perform private.process_economy_history_queue(25);
  if (select repricing from private.economy_history_publish_state where singleton) then raise exception 'Completed repricing not published'; end if;
  if (select count(*) from public.economy_group_wealth_history) <> v_old_count or
     (select sum(wealth) from public.economy_group_wealth_history) is distinct from v_before then raise exception 'Repricing changed unchanged historical results'; end if;
  if exists(select 1 from private.economy_wealth_history_staging) then raise exception 'Staging not cleared'; end if;
  raise exception using errcode='P9999',message='rollback successful atomic publication test';
 exception when sqlstate 'P9999' then null; end;
end $tests$;

