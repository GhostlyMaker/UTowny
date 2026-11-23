-- Preserve conflict locking without rewriting the queue row for every item
-- in the same ingestion transaction. No permissions are changed here.
set local lock_timeout='5s';
set local statement_timeout='30s';
do $coalesce$
declare p regprocedure; d text;
begin
 foreach p in array array[
  'private.queue_economy_snapshot_history()'::regprocedure,
  'private.queue_economy_item_history()'::regprocedure,
  'private.queue_economy_dimension_history()'::regprocedure
 ] loop
  d:=pg_get_functiondef(p);
  d:=replace(d,'do update set queued_at=clock_timestamp();',
   'do update set queued_at=clock_timestamp() where economy_history_queue.queued_at < transaction_timestamp();');
  execute d;
 end loop;
end $coalesce$;
comment on view public.economy_group_wealth_history is 'Read-only public aggregate interface. Owner access is intentional: private cache tables have no anonymous/authenticated grants. Contains the same approved public columns as the former materialized view.';
comment on view public.economy_category_history is 'Read-only public aggregate interface over private cache; same approved public columns as the former materialized view.';
comment on view public.economy_group_category_history is 'Read-only public aggregate interface over private cache; same approved public columns as the former materialized view.';

