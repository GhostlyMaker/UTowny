using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Persistence.Database;
using UTowny.Persistence.Repositories;
using UTowny.Utilities;
namespace UTowny.Services;

public interface IEconomyService { Task<Result<long>> TransferAsync(PlayerId sender,PlayerId recipient,long amount,CancellationToken ct=default); Task<long> GetBalanceAsync(PlayerId p,CancellationToken ct=default); Task<Result<long>> CreditAsync(PlayerId p,long amount,CancellationToken ct=default); Task<Result<long>> DebitAsync(PlayerId p,long amount,CancellationToken ct=default); }
public sealed class EconomyService:IEconomyService
{
 private readonly MutationGate m_Gate;

 private readonly IDatabaseConnectionFactory m_Db;private readonly IPlayerRepository m_Players;private readonly IOptions<UTownyOptions> m_Options;private readonly KeyedLock<ulong> m_Lock=new();
 public EconomyService(IDatabaseConnectionFactory db,IPlayerRepository players,IOptions<UTownyOptions> options,MutationGate gate){m_Gate=gate;m_Db=db;m_Players=players;m_Options=options;}
 public Task<long> GetBalanceAsync(PlayerId p,CancellationToken ct=default)=>m_Gate.RunAsync(async ()=>(await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct)).Balance,ct);
 public Task<Result<long>> CreditAsync(PlayerId p,long amount,CancellationToken ct=default) => m_Gate.RunAsync(async () => {if(amount<=0)return Result<long>.Fail("invalid_amount");using(await m_Lock.AcquireAsync(p.Value,ct)){await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);await using var db=await m_Db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="UPDATE players SET balance=balance+$a WHERE steam64=$p AND balance <= 9223372036854775807-$a RETURNING balance";c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$p",unchecked((long)p.Value));var v=await c.ExecuteScalarAsync(ct);return v is null?Result<long>.Fail("invalid_amount"):Result<long>.Ok((long)v);}}, ct,new UTowny.Api.Events.DomainOperation("economy.credit",p));
 public Task<Result<long>> DebitAsync(PlayerId p,long amount,CancellationToken ct=default) => m_Gate.RunAsync(async () => {if(amount<=0)return Result<long>.Fail("invalid_amount");using(await m_Lock.AcquireAsync(p.Value,ct)){await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);await using var db=await m_Db.OpenAsync(ct);await using var c=db.CreateCommand();c.CommandText="UPDATE players SET balance=balance-$a WHERE steam64=$p AND balance >= $a RETURNING balance";c.Parameters.AddWithValue("$a",amount);c.Parameters.AddWithValue("$p",unchecked((long)p.Value));var v=await c.ExecuteScalarAsync(ct);return v is null?Result<long>.Fail("insufficient_balance"):Result<long>.Ok((long)v);}}, ct,new UTowny.Api.Events.DomainOperation("economy.debit",p));
 public Task<Result<long>> TransferAsync(PlayerId sender,PlayerId recipient,long amount,CancellationToken ct=default)=>m_Gate.RunAsync(async()=>
 {
  if(amount<=0)return Result<long>.Fail("invalid_amount");
  if(sender==recipient)return Result<long>.Fail("pay_self");
  if(await m_Players.GetAsync(recipient,ct)==null)return Result<long>.Fail("invalid_player");
  await m_Players.EnsureAsync(sender,m_Options.Value.Economy.StartingBalance,ct);
  using var db=await m_Db.OpenAsync(ct);using var s=new SqlSession(db);
  if(s.Execute("UPDATE players SET balance=balance-$0 WHERE steam64=$1 AND balance>=$0",amount,(long)sender.Value)!=1)return Result<long>.Fail("insufficient_balance");
  if(s.Execute("UPDATE players SET balance=balance+$0 WHERE steam64=$1 AND balance<=9223372036854775807-$0",amount,(long)recipient.Value)!=1)return Result<long>.Fail("invalid_amount");
  var balance=s.Number("SELECT balance FROM players WHERE steam64=$0",(long)sender.Value);
  s.Commit();return Result<long>.Ok(balance);
 },ct,new UTowny.Api.Events.DomainOperation("economy.pay",sender,Detail:$"recipient={recipient.Value} amount={amount}"));

}
