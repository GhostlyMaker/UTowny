using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Persistence.Database;
using UTowny.Persistence.Repositories;
using UTowny.Utilities;
namespace UTowny.Services;

public interface IEconomyService { Task<long> GetBalanceAsync(PlayerId p,CancellationToken ct=default); Task<Result<long>> CreditAsync(PlayerId p,long amount,CancellationToken ct=default); Task<Result<long>> DebitAsync(PlayerId p,long amount,CancellationToken ct=default); }
public sealed class EconomyService:IEconomyService
{
 private readonly IDatabaseConnectionFactory m_Db;private readonly IPlayerRepository m_Players;private readonly IOptions<UTownyOptions> m_Options;private readonly KeyedLock<ulong> m_Lock=new();
 public EconomyService(IDatabaseConnectionFactory db,IPlayerRepository players,IOptions<UTownyOptions> options){m_Db=db;m_Players=players;m_Options=options;}
 public async Task<long> GetBalanceAsync(PlayerId p,CancellationToken ct=default)=>(await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct)).Balance;
 public async Task<Result<long>> CreditAsync(PlayerId p,long amount,CancellationToken ct=default){if(amount<=0)return Result<long>.Fail("invalid_amount");using(await m_Lock.AcquireAsync(p.Value,ct)){await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);await using var db=await m_Db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="UPDATE players SET balance=balance+$a WHERE steam64=$p RETURNING balance";c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$p",unchecked((long)p.Value));var v=(long)(await c.ExecuteScalarAsync(ct))!;return Result<long>.Ok(v);}}
 public async Task<Result<long>> DebitAsync(PlayerId p,long amount,CancellationToken ct=default){if(amount<=0)return Result<long>.Fail("invalid_amount");using(await m_Lock.AcquireAsync(p.Value,ct)){await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);await using var db=await m_Db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="UPDATE players SET balance=balance-$a WHERE steam64=$p AND balance >= $a RETURNING balance";c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$p",unchecked((long)p.Value));var v=await c.ExecuteScalarAsync(ct);return v is null?Result<long>.Fail("insufficient_balance"):Result<long>.Ok((long)v);}}
}
