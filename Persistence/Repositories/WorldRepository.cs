using Microsoft.Data.Sqlite;
using UTowny.Domain.Claims;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Persistence.Database;
namespace UTowny.Persistence.Repositories;

public interface IWorldRepository
{
 Task<IReadOnlyList<Town>> LoadTownsAsync(CancellationToken ct=default);
 Task<IReadOnlyList<TownMember>> LoadMembersAsync(CancellationToken ct=default);
 Task<IReadOnlyList<Claim>> LoadClaimsAsync(CancellationToken ct=default);
}

public sealed class WorldRepository : IWorldRepository
{
 private readonly IDatabaseConnectionFactory m_Db; public WorldRepository(IDatabaseConnectionFactory db)=>m_Db=db;
 public async Task<IReadOnlyList<Town>> LoadTownsAsync(CancellationToken ct=default)
 {
  var list=new List<Town>(); await using var db=await m_Db.OpenAsync(ct); await using var c=db.CreateCommand();
  c.CommandText="SELECT id,name,mayor_steam64,bank_balance,pvp_enabled,created_utc,next_upkeep_utc,tax_enabled,tax_amount,next_tax_utc,spawn_x,spawn_y,spawn_z,spawn_yaw,spawn_public FROM towns";
  await using var r=await c.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) { TownSpawn? s=r.IsDBNull(10)?null:new((float)r.GetDouble(10),(float)r.GetDouble(11),(float)r.GetDouble(12),(float)r.GetDouble(13)); list.Add(new Town(new(r.GetInt64(0)),r.GetString(1),new(unchecked((ulong)r.GetInt64(2))),r.GetInt64(3),r.GetInt64(4)!=0,Dt(r,5),Dt(r,6),r.GetInt64(7)!=0,r.GetInt64(8),Dt(r,9),s,r.GetInt64(14)!=0)); }
  return list;
 }
 public async Task<IReadOnlyList<TownMember>> LoadMembersAsync(CancellationToken ct=default)
 {
  var list=new List<TownMember>(); await using var db=await m_Db.OpenAsync(ct); await using var c=db.CreateCommand(); c.CommandText="SELECT town_id,player_steam64,role,joined_utc,missed_tax_cycles FROM town_members"; await using var r=await c.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) list.Add(new(new(r.GetInt64(0)),new(unchecked((ulong)r.GetInt64(1))),(TownRole)r.GetInt32(2),Dt(r,3),r.GetInt32(4))); return list;
 }
 public async Task<IReadOnlyList<Claim>> LoadClaimsAsync(CancellationToken ct=default)
 {
  var list=new List<Claim>(); await using var db=await m_Db.OpenAsync(ct); await using var c=db.CreateCommand(); c.CommandText="SELECT id,town_id,map_id,grid_x,grid_z,plot_owner_steam64,for_sale,price,protection_flags FROM claims"; await using var r=await c.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) list.Add(new(new(r.GetInt64(0)),new(r.GetInt64(1)),new(r.GetString(2),r.GetInt32(3),r.GetInt32(4)),r.IsDBNull(5)?null:new PlayerId(unchecked((ulong)r.GetInt64(5))),r.GetInt64(6)!=0,r.GetInt64(7),r.GetInt64(8))); return list;
 }
 private static DateTime Dt(SqliteDataReader r,int i)=>DateTime.Parse(r.GetString(i),null,System.Globalization.DateTimeStyles.RoundtripKind);
}
