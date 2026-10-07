using System.Collections.Concurrent;
using UTowny.Domain.Claims;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Persistence.Repositories;
namespace UTowny.Caching;

public interface IWorldStateCache
{
 Town? GetTown(TownId id); Town? GetTownByName(string name); TownMember? GetMembership(PlayerId id); Claim? GetClaim(GridCoord grid);
 IReadOnlyCollection<Claim> GetTownClaims(TownId id); int GetResidentCount(TownId id);
 void UpsertTown(Town town); void RemoveTown(TownId id); void UpsertMember(TownMember member); void RemoveMember(PlayerId player); void UpsertClaim(Claim claim); void RemoveClaim(GridCoord grid); Task RebuildAsync(CancellationToken ct=default);
}

public sealed class WorldStateCache : IWorldStateCache
{
 private readonly IWorldRepository m_Repo;
 private readonly ConcurrentDictionary<long,Town> m_Towns=new(); private readonly ConcurrentDictionary<string,long> m_TownNames=new(StringComparer.OrdinalIgnoreCase); private readonly ConcurrentDictionary<ulong,TownMember> m_Members=new(); private readonly ConcurrentDictionary<GridCoord,Claim> m_Claims=new();
 public WorldStateCache(IWorldRepository repo)=>m_Repo=repo;
 public Town? GetTown(TownId id)=>m_Towns.TryGetValue(id.Value,out var v)?v:null;
 public Town? GetTownByName(string name)=>m_TownNames.TryGetValue(name,out var id)?GetTown(new(id)):null;
 public TownMember? GetMembership(PlayerId id)=>m_Members.TryGetValue(id.Value,out var v)?v:null;
 public Claim? GetClaim(GridCoord grid)=>m_Claims.TryGetValue(grid,out var v)?v:null;
 public IReadOnlyCollection<Claim> GetTownClaims(TownId id)=>m_Claims.Values.Where(x=>x.TownId==id).ToArray();
 public int GetResidentCount(TownId id)=>m_Members.Values.Count(x=>x.TownId==id);
 public void UpsertTown(Town t){m_Towns[t.Id.Value]=t;m_TownNames[t.Name]=t.Id.Value;}
 public void RemoveTown(TownId id){if(m_Towns.TryRemove(id.Value,out var t))m_TownNames.TryRemove(t.Name,out _);foreach(var m in m_Members.Where(x=>x.Value.TownId==id).ToArray())m_Members.TryRemove(m.Key,out _);foreach(var c in m_Claims.Where(x=>x.Value.TownId==id).ToArray())m_Claims.TryRemove(c.Key,out _);}
 public void UpsertMember(TownMember m)=>m_Members[m.PlayerId.Value]=m; public void RemoveMember(PlayerId p)=>m_Members.TryRemove(p.Value,out _);
 public void UpsertClaim(Claim c)=>m_Claims[c.Grid]=c; public void RemoveClaim(GridCoord g)=>m_Claims.TryRemove(g,out _);
 public async Task RebuildAsync(CancellationToken ct=default){m_Towns.Clear();m_TownNames.Clear();m_Members.Clear();m_Claims.Clear();foreach(var t in await m_Repo.LoadTownsAsync(ct))UpsertTown(t);foreach(var m in await m_Repo.LoadMembersAsync(ct))UpsertMember(m);foreach(var c in await m_Repo.LoadClaimsAsync(ct))UpsertClaim(c);}
}
