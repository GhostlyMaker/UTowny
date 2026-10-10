using Microsoft.Extensions.Options;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Persistence.Database;
namespace UTowny.Services;
public enum WarStatus { Requested, Preparing, Active, Ended, Cancelled }
public sealed record War(long Id,TownId A,TownId B,TownId RequestedBy,DateTime? StartUtc,DateTime? EndUtc,WarStatus Status);
public interface IWarService
{
 IReadOnlyList<War> All { get; }
 bool AreTownsAtWar(TownId a,TownId b);
 Task<Result> ExecuteAsync(PlayerId actor,string action,TownId target);
 Task RefreshAsync();
}
public sealed class WarService : IWarService
{
 private readonly IDatabaseConnectionFactory m_Db;private readonly IWorldStateCache m_Cache;private readonly MutationGate m_Gate;private readonly IOptions<UTownyOptions> m_Options;
 private War[] m_Wars=Array.Empty<War>();
 public IReadOnlyList<War> All=>Volatile.Read(ref m_Wars);
 public WarService(IDatabaseConnectionFactory db,IWorldStateCache cache,MutationGate gate,IOptions<UTownyOptions> options){m_Db=db;m_Cache=cache;m_Gate=gate;m_Options=options;}
 public bool AreTownsAtWar(TownId a,TownId b)
 {
  var now=DateTime.UtcNow;
  return a!=b&&All.Any(w=>((w.A==a&&w.B==b)||(w.A==b&&w.B==a))&&(w.Status==WarStatus.Active||w.Status==WarStatus.Preparing)&&w.StartUtc<=now&&w.EndUtc>now);
 }
 public async Task RefreshAsync()
 {
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
  var all=s.Rows("SELECT id,a,b,requested_by,start_utc,end_utc,status FROM wars").Select(r=>new War(Convert.ToInt64(r[0]),new(Convert.ToInt64(r[1])),new(Convert.ToInt64(r[2])),new(Convert.ToInt64(r[3])),Parse(r[4]),Parse(r[5]),(WarStatus)Convert.ToInt32(r[6]))).ToArray();
  Volatile.Write(ref m_Wars,all);
 }
 private static DateTime? Parse(object? value)=>value==null?null:DateTime.Parse((string)value,null,System.Globalization.DateTimeStyles.RoundtripKind);
 public Task<Result> ExecuteAsync(PlayerId actor,string action,TownId target)=>m_Gate.RunAsync(async()=>
 {
  var member=m_Cache.GetMembership(actor);if(member==null)return Result.Fail("not_in_town");if(member.Role!=TownRole.Mayor)return Result.Fail("not_mayor");
  var town=member.TownId;if(town==target||m_Cache.GetTown(target)==null)return Result.Fail("invalid_target");
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
  var a=Math.Min(town.Value,target.Value);var b=Math.Max(town.Value,target.Value);var now=DateTime.UtcNow;
  if(action=="request")
  {
   if(s.Number("SELECT COUNT(*) FROM wars WHERE a=$0 AND b=$1 AND status IN (0,1,2)",a,b)!=0)return Result.Fail("request_exists");
   s.Execute("INSERT INTO wars(a,b,requested_by,requested_utc,status) VALUES($0,$1,$2,$3,0)",a,b,town.Value,now.ToString("O"));
  }
  else if(action=="accept")
  {
   var start=now.AddHours(m_Options.Value.Wars.PreparationHours);var end=start.AddMinutes(m_Options.Value.Wars.DurationMinutes);
   if(s.Execute("UPDATE wars SET accepted_utc=$0,start_utc=$1,end_utc=$2,status=1 WHERE a=$3 AND b=$4 AND requested_by=$5 AND status=0",now.ToString("O"),start.ToString("O"),end.ToString("O"),a,b,target.Value)!=1)return Result.Fail("invite_missing");
  }
  else if(action=="decline")
  {if(s.Execute("UPDATE wars SET status=4 WHERE a=$0 AND b=$1 AND requested_by=$2 AND status=0",a,b,target.Value)!=1)return Result.Fail("invite_missing");}
  else if(action=="cancel")
  {if(s.Execute("UPDATE wars SET status=4 WHERE a=$0 AND b=$1 AND requested_by=$2 AND status=0",a,b,town.Value)!=1)return Result.Fail("war_cannot_cancel");}
  else return Result.Fail("syntax");
  s.Commit();await RefreshAsync();return Result.Ok();
 },change:new UTowny.Api.Events.DomainOperation("war."+action,actor));
}
