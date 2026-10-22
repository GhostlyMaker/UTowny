create extension if not exists pgcrypto;

create table if not exists public.economy_servers (
  id text primary key,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.economy_groups (
  server_id text not null references public.economy_servers(id) on delete cascade,
  group_id text not null,
  name text not null default '',
  first_seen_at timestamptz not null default now(),
  last_seen_at timestamptz not null default now(),
  primary key (server_id, group_id)
);

create table if not exists public.economy_items (
  item_key text primary key,
  item_id integer not null,
  item_guid text,
  name text not null default '',
  item_type text not null default '',
  first_seen_at timestamptz not null default now(),
  last_seen_at timestamptz not null default now()
);

create unique index if not exists economy_items_guid_unique
  on public.economy_items (lower(item_guid))
  where item_guid is not null and item_guid <> '';

create table if not exists public.economy_snapshots (
  id uuid primary key default gen_random_uuid(),
  server_id text not null references public.economy_servers(id) on delete cascade,
  schema_version integer not null,
  generated_at timestamptz not null,
  received_at timestamptz not null default now(),
  trigger text not null default '',
  scope_note text not null default '',
  barricades_visited bigint not null default 0,
  storage_containers_visited bigint not null default 0,
  online_players_visited bigint not null default 0,
  total_item_instances bigint not null default 0,
  player_item_instances bigint not null default 0,
  storage_item_instances bigint not null default 0,
  wall_clock_ms double precision not null default 0,
  active_work_ms double precision not null default 0,
  raw_payload jsonb not null,
  unique (server_id, generated_at)
);

create index if not exists economy_snapshots_server_generated_idx
  on public.economy_snapshots (server_id, generated_at desc);

create table if not exists public.economy_group_snapshots (
  snapshot_id uuid not null references public.economy_snapshots(id) on delete cascade,
  server_id text not null,
  group_id text not null,
  group_name text not null default '',
  online_players integer not null default 0,
  storage_containers bigint not null default 0,
  player_item_instances bigint not null default 0,
  storage_item_instances bigint not null default 0,
  primary key (snapshot_id, group_id),
  foreign key (server_id, group_id)
    references public.economy_groups(server_id, group_id)
    on delete cascade
);

create index if not exists economy_group_snapshots_group_idx
  on public.economy_group_snapshots (server_id, group_id, snapshot_id);

create table if not exists public.economy_snapshot_items (
  snapshot_id uuid not null references public.economy_snapshots(id) on delete cascade,
  server_id text not null,
  group_id text not null,
  item_key text not null references public.economy_items(item_key),
  instances bigint not null default 0,
  contained_amount bigint not null default 0,
  player_instances bigint not null default 0,
  storage_instances bigint not null default 0,
  primary key (snapshot_id, group_id, item_key),
  foreign key (server_id, group_id)
    references public.economy_groups(server_id, group_id)
    on delete cascade
);

create index if not exists economy_snapshot_items_item_idx
  on public.economy_snapshot_items (server_id, item_key, snapshot_id);

create table if not exists public.economy_item_prices (
  id bigint generated always as identity primary key,
  item_key text not null references public.economy_items(item_key) on delete cascade,
  value numeric(20,4) not null check (value >= 0),
  effective_from timestamptz not null default now(),
  note text not null default '',
  created_at timestamptz not null default now()
);

create index if not exists economy_item_prices_lookup_idx
  on public.economy_item_prices (item_key, effective_from desc);

create or replace function public.ingest_economy_snapshot(payload jsonb)
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_server_id text;
  v_generated_at timestamptz;
  v_schema_version integer;
  v_snapshot_id uuid;
  v_group jsonb;
  v_item jsonb;
  v_group_id text;
  v_guid text;
  v_item_key text;
begin
  if payload is null or jsonb_typeof(payload) <> 'object' then
    raise exception 'payload must be a JSON object';
  end if;

  v_schema_version := coalesce((payload->>'schemaVersion')::integer, 0);
  if v_schema_version <> 2 then
    raise exception 'unsupported schemaVersion: %', v_schema_version;
  end if;

  v_server_id := nullif(btrim(payload->>'serverId'), '');
  if v_server_id is null then
    raise exception 'serverId is required';
  end if;

  v_generated_at := (payload->>'generatedAtUtc')::timestamptz;
  if v_generated_at is null then
    raise exception 'generatedAtUtc is required';
  end if;

  if jsonb_typeof(coalesce(payload->'groups', '[]'::jsonb)) <> 'array' then
    raise exception 'groups must be an array';
  end if;

  insert into public.economy_servers(id, updated_at)
  values (v_server_id, now())
  on conflict (id) do update set updated_at = excluded.updated_at;

  insert into public.economy_snapshots (
    server_id, schema_version, generated_at, received_at, trigger, scope_note,
    barricades_visited, storage_containers_visited, online_players_visited,
    total_item_instances, player_item_instances, storage_item_instances,
    wall_clock_ms, active_work_ms, raw_payload
  ) values (
    v_server_id,
    v_schema_version,
    v_generated_at,
    now(),
    coalesce(payload->>'trigger', ''),
    coalesce(payload->>'scopeNote', ''),
    coalesce((payload#>>'{stats,barricadesVisited}')::bigint, 0),
    coalesce((payload#>>'{stats,storageContainersVisited}')::bigint, 0),
    coalesce((payload#>>'{stats,onlinePlayersVisited}')::bigint, 0),
    coalesce((payload#>>'{stats,totalItemInstances}')::bigint, 0),
    coalesce((payload#>>'{stats,playerItemInstances}')::bigint, 0),
    coalesce((payload#>>'{stats,storageItemInstances}')::bigint, 0),
    coalesce((payload#>>'{stats,wallClockMilliseconds}')::double precision, 0),
    coalesce((payload#>>'{stats,activeWorkMilliseconds}')::double precision, 0),
    payload
  )
  on conflict (server_id, generated_at) do update set
    received_at = now(),
    trigger = excluded.trigger,
    scope_note = excluded.scope_note,
    barricades_visited = excluded.barricades_visited,
    storage_containers_visited = excluded.storage_containers_visited,
    online_players_visited = excluded.online_players_visited,
    total_item_instances = excluded.total_item_instances,
    player_item_instances = excluded.player_item_instances,
    storage_item_instances = excluded.storage_item_instances,
    wall_clock_ms = excluded.wall_clock_ms,
    active_work_ms = excluded.active_work_ms,
    raw_payload = excluded.raw_payload
  returning id into v_snapshot_id;

  delete from public.economy_snapshot_items where snapshot_id = v_snapshot_id;
  delete from public.economy_group_snapshots where snapshot_id = v_snapshot_id;

  for v_group in
    select value from jsonb_array_elements(coalesce(payload->'groups', '[]'::jsonb))
  loop
    v_group_id := nullif(btrim(v_group->>'groupId'), '');
    if v_group_id is null then
      raise exception 'groupId is required';
    end if;

    insert into public.economy_groups (
      server_id, group_id, name, first_seen_at, last_seen_at
    ) values (
      v_server_id,
      v_group_id,
      coalesce(v_group->>'groupName', ''),
      v_generated_at,
      v_generated_at
    )
    on conflict (server_id, group_id) do update set
      name = excluded.name,
      last_seen_at = greatest(public.economy_groups.last_seen_at, excluded.last_seen_at);

    insert into public.economy_group_snapshots (
      snapshot_id, server_id, group_id, group_name, online_players,
      storage_containers, player_item_instances, storage_item_instances
    ) values (
      v_snapshot_id,
      v_server_id,
      v_group_id,
      coalesce(v_group->>'groupName', ''),
      coalesce((v_group->>'onlinePlayers')::integer, 0),
      coalesce((v_group->>'storageContainers')::bigint, 0),
      coalesce((v_group->>'playerItemInstances')::bigint, 0),
      coalesce((v_group->>'storageItemInstances')::bigint, 0)
    );

    if jsonb_typeof(coalesce(v_group->'items', '[]'::jsonb)) <> 'array' then
      raise exception 'items must be an array for groupId %', v_group_id;
    end if;

    for v_item in
      select value from jsonb_array_elements(coalesce(v_group->'items', '[]'::jsonb))
    loop
      v_guid := nullif(lower(btrim(coalesce(v_item->>'guid', ''))), '');
      v_item_key := coalesce(v_guid, 'legacy:' || coalesce(v_item->>'itemId', '0'));

      insert into public.economy_items (
        item_key, item_id, item_guid, name, item_type, first_seen_at, last_seen_at
      ) values (
        v_item_key,
        coalesce((v_item->>'itemId')::integer, 0),
        v_guid,
        coalesce(v_item->>'name', ''),
        coalesce(v_item->>'itemType', ''),
        v_generated_at,
        v_generated_at
      )
      on conflict (item_key) do update set
        item_id = excluded.item_id,
        item_guid = excluded.item_guid,
        name = excluded.name,
        item_type = excluded.item_type,
        last_seen_at = greatest(public.economy_items.last_seen_at, excluded.last_seen_at);

      insert into public.economy_snapshot_items (
        snapshot_id, server_id, group_id, item_key, instances, contained_amount,
        player_instances, storage_instances
      ) values (
        v_snapshot_id,
        v_server_id,
        v_group_id,
        v_item_key,
        coalesce((v_item->>'instances')::bigint, 0),
        coalesce((v_item->>'containedAmount')::bigint, 0),
        coalesce((v_item->>'playerInstances')::bigint, 0),
        coalesce((v_item->>'storageInstances')::bigint, 0)
      );
    end loop;
  end loop;

  return v_snapshot_id;
end;
$$;

revoke all on function public.ingest_economy_snapshot(jsonb) from public;
revoke all on function public.ingest_economy_snapshot(jsonb) from anon;
revoke all on function public.ingest_economy_snapshot(jsonb) from authenticated;
grant execute on function public.ingest_economy_snapshot(jsonb) to service_role;

create or replace view public.economy_latest_snapshots
with (security_invoker = true)
as
select distinct on (server_id)
  id, server_id, schema_version, generated_at, received_at, trigger, scope_note,
  barricades_visited, storage_containers_visited, online_players_visited,
  total_item_instances, player_item_instances, storage_item_instances,
  wall_clock_ms, active_work_ms
from public.economy_snapshots
order by server_id, generated_at desc;

create or replace view public.economy_current_group_totals
with (security_invoker = true)
as
select
  s.server_id,
  s.generated_at,
  gs.group_id,
  gs.group_name,
  gs.online_players,
  gs.storage_containers,
  gs.player_item_instances,
  gs.storage_item_instances,
  gs.player_item_instances + gs.storage_item_instances as total_item_instances
from public.economy_latest_snapshots s
join public.economy_group_snapshots gs on gs.snapshot_id = s.id;

create or replace view public.economy_current_item_control
with (security_invoker = true)
as
with current_rows as (
  select
    s.server_id,
    s.generated_at,
    si.group_id,
    si.item_key,
    si.instances,
    si.contained_amount,
    sum(si.instances) over (partition by s.server_id, si.item_key) as server_instances,
    sum(si.contained_amount) over (partition by s.server_id, si.item_key) as server_contained_amount
  from public.economy_latest_snapshots s
  join public.economy_snapshot_items si on si.snapshot_id = s.id
)
select
  c.server_id,
  c.generated_at,
  c.group_id,
  g.name as group_name,
  c.item_key,
  i.item_id,
  i.item_guid,
  i.name as item_name,
  i.item_type,
  c.instances,
  c.contained_amount,
  c.server_instances,
  c.server_contained_amount,
  case when c.server_instances > 0
    then round((c.instances::numeric / c.server_instances::numeric) * 100, 4)
    else 0 end as instance_control_pct,
  case when c.server_contained_amount > 0
    then round((c.contained_amount::numeric / c.server_contained_amount::numeric) * 100, 4)
    else 0 end as contained_amount_control_pct
from current_rows c
join public.economy_groups g
  on g.server_id = c.server_id and g.group_id = c.group_id
join public.economy_items i on i.item_key = c.item_key;

create or replace view public.economy_current_group_wealth
with (security_invoker = true)
as
with latest_prices as (
  select distinct on (item_key)
    item_key,
    value
  from public.economy_item_prices
  order by item_key, effective_from desc, id desc
), current_holdings as (
  select
    s.server_id,
    s.generated_at,
    si.group_id,
    si.item_key,
    si.instances,
    coalesce(lp.value, 0) as unit_value
  from public.economy_latest_snapshots s
  join public.economy_snapshot_items si on si.snapshot_id = s.id
  left join latest_prices lp on lp.item_key = si.item_key
), totals as (
  select
    server_id,
    generated_at,
    group_id,
    sum(instances::numeric * unit_value) as wealth
  from current_holdings
  group by server_id, generated_at, group_id
)
select
  t.server_id,
  t.generated_at,
  t.group_id,
  g.name as group_name,
  t.wealth,
  case when sum(t.wealth) over (partition by t.server_id) > 0
    then round((t.wealth / sum(t.wealth) over (partition by t.server_id)) * 100, 4)
    else 0 end as economy_share_pct
from totals t
join public.economy_groups g
  on g.server_id = t.server_id and g.group_id = t.group_id;

alter table public.economy_servers enable row level security;
alter table public.economy_groups enable row level security;
alter table public.economy_items enable row level security;
alter table public.economy_snapshots enable row level security;
alter table public.economy_group_snapshots enable row level security;
alter table public.economy_snapshot_items enable row level security;
alter table public.economy_item_prices enable row level security;

drop policy if exists "economy public read servers" on public.economy_servers;
create policy "economy public read servers" on public.economy_servers for select to anon, authenticated using (true);

drop policy if exists "economy public read groups" on public.economy_groups;
create policy "economy public read groups" on public.economy_groups for select to anon, authenticated using (true);

drop policy if exists "economy public read items" on public.economy_items;
create policy "economy public read items" on public.economy_items for select to anon, authenticated using (true);

drop policy if exists "economy public read snapshots" on public.economy_snapshots;
create policy "economy public read snapshots" on public.economy_snapshots for select to anon, authenticated using (true);

drop policy if exists "economy public read group snapshots" on public.economy_group_snapshots;
create policy "economy public read group snapshots" on public.economy_group_snapshots for select to anon, authenticated using (true);

drop policy if exists "economy public read snapshot items" on public.economy_snapshot_items;
create policy "economy public read snapshot items" on public.economy_snapshot_items for select to anon, authenticated using (true);

drop policy if exists "economy public read prices" on public.economy_item_prices;
create policy "economy public read prices" on public.economy_item_prices for select to anon, authenticated using (true);

grant select on public.economy_servers to anon, authenticated;
grant select on public.economy_groups to anon, authenticated;
grant select on public.economy_items to anon, authenticated;
grant select on public.economy_snapshots to anon, authenticated;
grant select on public.economy_group_snapshots to anon, authenticated;
grant select on public.economy_snapshot_items to anon, authenticated;
grant select on public.economy_item_prices to anon, authenticated;
grant select on public.economy_latest_snapshots to anon, authenticated;
grant select on public.economy_current_group_totals to anon, authenticated;
grant select on public.economy_current_item_control to anon, authenticated;
grant select on public.economy_current_group_wealth to anon, authenticated;
