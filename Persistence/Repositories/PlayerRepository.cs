using Microsoft.Data.Sqlite;
using UTowny.Domain.Common;
using UTowny.Domain.Players;
using UTowny.Persistence.Database;
namespace UTowny.Persistence.Repositories;

public interface IPlayerRepository
{
    Task<PlayerProfile> EnsureAsync(PlayerId id, long startingBalance, CancellationToken ct=default);
    Task<PlayerProfile?> GetAsync(PlayerId id, CancellationToken ct=default);
    Task AddPlaytimeAsync(PlayerId id, long seconds, DateTime lastSeenUtc, CancellationToken ct=default);
}

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly IDatabaseConnectionFactory m_Db;
    public PlayerRepository(IDatabaseConnectionFactory db)=>m_Db=db;
    public async Task<PlayerProfile> EnsureAsync(PlayerId id, long startingBalance, CancellationToken ct=default)
    {
        await using var db=await m_Db.OpenAsync(ct);
        await using(var cmd=db.CreateCommand()) { cmd.CommandText="INSERT OR IGNORE INTO players(steam64,balance,playtime_seconds,last_seen_utc) VALUES($id,$bal,0,$now)"; cmd.Parameters.AddWithValue("$id", unchecked((long)id.Value)); cmd.Parameters.AddWithValue("$bal",startingBalance); cmd.Parameters.AddWithValue("$now",DateTime.UtcNow.ToString("O")); await cmd.ExecuteNonQueryAsync(ct); }
        return (await GetAsync(id,ct))!;
    }
    public async Task<PlayerProfile?> GetAsync(PlayerId id, CancellationToken ct=default)
    {
        await using var db=await m_Db.OpenAsync(ct); await using var cmd=db.CreateCommand();
        cmd.CommandText="SELECT balance,playtime_seconds,last_seen_utc FROM players WHERE steam64=$id"; cmd.Parameters.AddWithValue("$id",unchecked((long)id.Value));
        await using var r=await cmd.ExecuteReaderAsync(ct); if(!await r.ReadAsync(ct)) return null;
        return new PlayerProfile(id,r.GetInt64(0),r.GetInt64(1),DateTime.Parse(r.GetString(2),null,System.Globalization.DateTimeStyles.RoundtripKind));
    }
    public async Task AddPlaytimeAsync(PlayerId id,long seconds,DateTime lastSeenUtc,CancellationToken ct=default)
    {
        if(seconds<=0)return; await using var db=await m_Db.OpenAsync(ct); await using var cmd=db.CreateCommand();
        cmd.CommandText="UPDATE players SET playtime_seconds=playtime_seconds+$s,last_seen_utc=$now WHERE steam64=$id"; cmd.Parameters.AddWithValue("$s",seconds);cmd.Parameters.AddWithValue("$now",lastSeenUtc.ToString("O"));cmd.Parameters.AddWithValue("$id",unchecked((long)id.Value)); await cmd.ExecuteNonQueryAsync(ct);
    }
}
