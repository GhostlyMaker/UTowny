create table public.economy_player_presence(
 server_id text primary key references public.economy_servers(id),
 steam_ids text[] not null default '{}',
 observed_at timestamptz not null,
 received_at timestamptz not null default now(),
 constraint presence_main check(server_id='main'),
 constraint presence_size check(cardinality(steam_ids)<=1024)
);
alter table public.economy_player_presence enable row level security;
revoke all on public.economy_player_presence from public,anon,authenticated;
grant select on public.economy_player_presence to anon,authenticated;
create policy presence_public_read on public.economy_player_presence for select to anon,authenticated using(server_id='main');
create function private.ingest_economy_presence_with_key(p_server_id text,p_steam_ids text[],p_observed_at timestamptz,p_supplied_key text) returns jsonb
language plpgsql security definer set search_path='' as $$
begin
 if nullif(p_supplied_key,'') is null or not exists(select 1 from private.economy_ingest_keys k where k.server_id=p_server_id and k.active and k.key_hash=encode(extensions.digest(p_supplied_key,'sha256'),'hex')) then raise exception 'unauthorized'; end if;
 if p_server_id is distinct from 'main' or p_steam_ids is null or cardinality(p_steam_ids)>1024
 or p_observed_at is null or p_observed_at>now()+interval '2 minutes' or p_observed_at<now()-interval '10 minutes'
 or exists(select 1 from unnest(p_steam_ids) s where s is null or s !~ '^[0-9]{17,20}$')
 then raise exception 'invalid_request'; end if;
 insert into public.economy_player_presence(server_id,steam_ids,observed_at,received_at)
 values(p_server_id,array(select distinct s from unnest(p_steam_ids) s order by s),p_observed_at,now())
 on conflict(server_id) do update set steam_ids=excluded.steam_ids,observed_at=excluded.observed_at,received_at=now()
 where excluded.observed_at>economy_player_presence.observed_at;
 return jsonb_build_object('ok',true,'accepted',found);
end $$;
create function public.ingest_economy_presence_with_key(p_server_id text,p_steam_ids text[],p_observed_at timestamptz,p_supplied_key text) returns jsonb
language sql set search_path='' as $$ select private.ingest_economy_presence_with_key(p_server_id,p_steam_ids,p_observed_at,p_supplied_key) $$;
revoke all on function private.ingest_economy_presence_with_key(text,text[],timestamptz,text) from public,anon,authenticated;
revoke all on function public.ingest_economy_presence_with_key(text,text[],timestamptz,text) from public,anon,authenticated;
grant execute on function private.ingest_economy_presence_with_key(text,text[],timestamptz,text) to service_role;
grant execute on function public.ingest_economy_presence_with_key(text,text[],timestamptz,text) to service_role;