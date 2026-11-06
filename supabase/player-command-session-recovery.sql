-- Verified browser identities may access a player without transferring the original account.
create table private.economy_player_sessions (
 server_id text not null, user_id uuid not null references auth.users(id) on delete cascade,
 steam_id text not null, verified_at timestamptz not null default now(),
 primary key(server_id,user_id),
 foreign key(server_id,steam_id) references private.economy_player_owners(server_id,steam_id) on delete cascade
);
alter table private.economy_player_sessions enable row level security;
revoke all on private.economy_player_sessions from public,anon,authenticated;
create index economy_player_sessions_player_idx on private.economy_player_sessions(server_id,steam_id);
CREATE OR REPLACE FUNCTION private.create_economy_player_link_code(p_server_id text, p_steam_id text)
 RETURNS TABLE(code text, expires_at timestamp with time zone)
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO ''
AS $function$
declare u uuid:=auth.uid(); c text; e timestamptz:=now()+interval '10 minutes';
begin
 if u is null then raise exception 'authentication_required'; end if;
 perform pg_advisory_xact_lock(hashtextextended('player-link:'||u::text,0));
 if p_server_id is distinct from 'main' or not exists(select 1 from public.economy_players p where p.server_id=p_server_id and p.steam_id=p_steam_id) then raise exception 'player_not_found'; end if;
 if exists(select 1 from private.economy_player_owners o where o.server_id=p_server_id and o.user_id=u and o.steam_id<>p_steam_id) then raise exception 'player_already_linked'; end if;
 if exists(select 1 from private.economy_player_sessions s where s.server_id=p_server_id and s.user_id=u and s.steam_id<>p_steam_id) then raise exception 'player_already_linked'; end if;
 if exists(select 1 from private.economy_player_link_codes l where l.user_id=u and l.created_at>now()-interval '30 seconds') then raise exception 'rate_limited'; end if;
 update private.economy_player_link_codes l set expires_at=least(l.expires_at,now()) where l.user_id=u and l.used_at is null;
 c:=upper(encode(extensions.gen_random_bytes(6),'hex'));
 insert into private.economy_player_link_codes(server_id,steam_id,user_id,code_hash,expires_at)
 values(p_server_id,p_steam_id,u,encode(extensions.digest(c,'sha256'),'hex'),e);
 return query select c,e;
end $function$
;
CREATE OR REPLACE FUNCTION private.verify_economy_player_link_with_key(p_server_id text, p_steam_id text, p_code text, p_supplied_key text)
 RETURNS jsonb
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO ''
AS $function$
declare l private.economy_player_link_codes; u uuid;
begin
 if nullif(p_supplied_key,'') is null or not exists(select 1 from private.economy_ingest_keys k where k.server_id=p_server_id and k.active and k.key_hash=encode(extensions.digest(p_supplied_key,'sha256'),'hex')) then raise exception 'unauthorized'; end if;
 if p_server_id is distinct from 'main' or p_steam_id is null or p_steam_id !~ '^[0-9]{17,20}$' or p_code is null or upper(btrim(p_code)) !~ '^[A-F0-9]{12}$' then raise exception 'invalid_request'; end if;
 select c.user_id into u from private.economy_player_link_codes c where c.server_id=p_server_id and c.steam_id=p_steam_id and c.code_hash=encode(extensions.digest(upper(btrim(p_code)),'sha256'),'hex');
 if u is null then raise exception 'invalid_or_expired_code'; end if;
 perform pg_advisory_xact_lock(hashtextextended('player-link:'||u::text,0));
 select c.* into l from private.economy_player_link_codes c where c.server_id=p_server_id and c.steam_id=p_steam_id and c.code_hash=encode(extensions.digest(upper(btrim(p_code)),'sha256'),'hex') and c.used_at is null and c.expires_at>now() for update;
 if l.id is null then raise exception 'invalid_or_expired_code'; end if;
 perform pg_advisory_xact_lock(hashtextextended('player-identity:'||p_server_id||':'||p_steam_id,0));
 if exists(select 1 from private.economy_player_sessions s where s.server_id=p_server_id and s.user_id=l.user_id and s.steam_id<>p_steam_id) then raise exception 'player_already_linked'; end if;
 if exists(select 1 from private.economy_player_owners o where o.server_id=p_server_id and o.user_id=l.user_id and o.steam_id<>p_steam_id) then raise exception 'player_already_linked'; end if;
 begin
 insert into private.economy_player_owners(server_id,steam_id,user_id) values(p_server_id,p_steam_id,l.user_id) on conflict(server_id,steam_id) do nothing;
 exception when unique_violation then raise exception 'player_already_linked'; end;
 insert into public.economy_player_profiles(server_id,steam_id) values(p_server_id,p_steam_id) on conflict(server_id,steam_id) do nothing;
 insert into private.economy_player_sessions(server_id,user_id,steam_id) values(p_server_id,l.user_id,p_steam_id) on conflict(server_id,user_id) do update set verified_at=now();
 update private.economy_player_link_codes c set used_at=now() where c.id=l.id;
 return jsonb_build_object('ok',true,'server_id',p_server_id,'steam_id',p_steam_id);
end $function$
;
CREATE OR REPLACE FUNCTION private.get_my_economy_player_profile()
 RETURNS SETOF economy_public_player_profiles
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO ''
AS $function$
begin
 if auth.uid() is null then raise exception 'authentication_required'; end if;
 return query select p.* from public.economy_public_player_profiles p join private.economy_player_owners o using(server_id,steam_id) where o.server_id='main' and (o.user_id=auth.uid() or exists(select 1 from private.economy_player_sessions s where s.server_id=o.server_id and s.steam_id=o.steam_id and s.user_id=auth.uid()));
end $function$
;
CREATE OR REPLACE FUNCTION private.update_economy_player_profile(p_bio text, p_role text, p_avatar jsonb, p_accent_color text)
 RETURNS economy_public_player_profiles
 LANGUAGE plpgsql
 SECURITY DEFINER
 SET search_path TO ''
AS $function$
declare u uuid:=auth.uid(); r public.economy_public_player_profiles;
begin
 if u is null then raise exception 'authentication_required'; end if;
 if p_bio is null or char_length(btrim(p_bio))>200 or (p_role is not null and p_role not in ('Builder','Raider','Trader','Farmer','Scavenger'))
 or not private.valid_economy_avatar(p_avatar) or p_accent_color is null or p_accent_color !~ '^#[0-9a-fA-F]{6}$' then raise exception 'invalid_profile'; end if;
 update public.economy_player_profiles p set bio=btrim(p_bio),role=p_role,avatar=p_avatar,accent_color=lower(p_accent_color),updated_at=now()
 from private.economy_player_owners o where p.server_id=o.server_id and p.steam_id=o.steam_id and o.server_id='main' and (o.user_id=u or exists(select 1 from private.economy_player_sessions s where s.server_id=o.server_id and s.steam_id=o.steam_id and s.user_id=u)) and p.is_verified
 returning p.* into r;
 if not found then raise exception 'not_player_owner'; end if;
 return r;
end $function$
;
