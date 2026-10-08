using UTowny.Domain.Claims;using UTowny.Domain.Common;using UTowny.Domain.Towns;using UTowny.Persistence.Repositories;
namespace UTowny.Caching;
public interface IWorldStateCache
{
 Town? GetTown(TownId id);Town? GetTownByName(string name);TownMember? GetMembership(PlayerId id);Claim? GetClaim(GridCoord grid);
 IReadOnlyCollection<Town> GetTowns();IReadOnlyCollection<TownMember> GetMembers(TownId id);IReadOnlyCollection<Claim> GetTownClaims(TownId id);int GetResidentCount(TownId id);
 void UpsertTown(Town town);void RemoveTown(TownId id);void UpsertMember(TownMember member);void RemoveMember(PlayerId player);void UpsertClaim(Claim claim);void RemoveClaim(GridCoord grid);Task RebuildAsync(CancellationToken ct=default);
 void BeginMutation();void CompleteMutation(bool publish);
}
public sealed class WorldStateCache:IWorldStateCache
{
 private sealed class Snapshot
 {
  public Dictionary<long,Town> Towns=new();public Dictionary<ulong,TownMember> Members=new();public Dictionary<GridCoord,Claim> Claims=new();
  public Snapshot Copy()=>new(){Towns=new(Towns),Members=new(Members),Claims=new(Claims)};
 }
 private readonly IWorldRepository m_Repo;private Snapshot m_Current=new();private readonly AsyncLocal<Snapshot?> m_Staged=new();
 private Snapshot Current=>m_Staged.Value??Volatile.Read(ref m_Current);
 public WorldStateCache(IWorldRepository repo)=>m_Repo=repo;
 public void BeginMutation()=>m_Staged.Value=Volatile.Read(ref m_Current).Copy();
 public void CompleteMutation(bool publish){if(publish&&m_Staged.Value!=null)Volatile.Write(ref m_Current,m_Staged.Value);m_Staged.Value=null;}
 public Town? GetTown(TownId id)=>Current.Towns.TryGetValue(id.Value,out var t)?t:null;
 public Town? GetTownByName(string name)=>Current.Towns.Values.FirstOrDefault(t=>string.Equals(t.Name,name,StringComparison.OrdinalIgnoreCase));
 public TownMember? GetMembership(PlayerId id)=>Current.Members.TryGetValue(id.Value,out var m)?m:null;
 public Claim? GetClaim(GridCoord grid)=>Current.Claims.TryGetValue(grid,out var c)?c:null;
 public IReadOnlyCollection<Town> GetTowns()=>Current.Towns.Values.ToArray();
 public IReadOnlyCollection<TownMember> GetMembers(TownId id)=>Current.Members.Values.Where(m=>m.TownId==id).ToArray();
 public IReadOnlyCollection<Claim> GetTownClaims(TownId id)=>Current.Claims.Values.Where(c=>c.TownId==id).ToArray();
 public int GetResidentCount(TownId id)=>Current.Members.Values.Count(m=>m.TownId==id);
 private Snapshot Writable=>m_Staged.Value??throw new InvalidOperationException("Cache mutations require MutationGate");
 public void UpsertTown(Town t)=>Writable.Towns[t.Id.Value]=t;
 public void UpsertMember(TownMember m)=>Writable.Members[m.PlayerId.Value]=m;
 public void UpsertClaim(Claim c)=>Writable.Claims[c.Grid]=c;
 public void RemoveClaim(GridCoord grid)=>Writable.Claims.Remove(grid);
 public void RemoveMember(PlayerId id)=>Writable.Members.Remove(id.Value);
 public void RemoveTown(TownId id){var s=Writable;s.Towns.Remove(id.Value);foreach(var m in s.Members.Values.Where(m=>m.TownId==id).ToArray())s.Members.Remove(m.PlayerId.Value);foreach(var c in s.Claims.Values.Where(c=>c.TownId==id).ToArray())s.Claims.Remove(c.Grid);}
 public async Task RebuildAsync(CancellationToken ct=default)
 {
  var towns=await m_Repo.LoadTownsAsync(ct);var members=await m_Repo.LoadMembersAsync(ct);var claims=await m_Repo.LoadClaimsAsync(ct);
  var next=new Snapshot{Towns=towns.ToDictionary(t=>t.Id.Value),Members=members.ToDictionary(m=>m.PlayerId.Value),Claims=claims.ToDictionary(c=>c.Grid)};
  // AsyncLocal assignment does not propagate to callers; update the existing transaction snapshot.
  if(m_Staged.Value is Snapshot staged){staged.Towns=next.Towns;staged.Members=next.Members;staged.Claims=next.Claims;}
  else Volatile.Write(ref m_Current,next);
 }
}
