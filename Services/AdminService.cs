using Microsoft.Extensions.Logging;
using UTowny.Caching;using UTowny.Domain.Claims;using UTowny.Domain.Common;using UTowny.Domain.Towns;using UTowny.Persistence.Database;
namespace UTowny.Services;
public sealed class AdminService
{
 private readonly IDatabaseConnectionFactory m_Db;private readonly IWorldStateCache m_Cache;private readonly MutationGate m_Gate;private readonly IWarService m_Wars;private readonly ILogger<AdminService> m_Log;
 public AdminService(IDatabaseConnectionFactory db,IWorldStateCache cache,MutationGate gate,IWarService wars,ILogger<AdminService> log){m_Db=db;m_Cache=cache;m_Gate=gate;m_Wars=wars;m_Log=log;}
 public Task<string> PendingAsync(string player)=>m_Gate.RunAsync(async()=>
 {
  if(!long.TryParse(player,out var id))return "-";
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
  return string.Join(" | ",s.Rows("SELECT id,kind,asset,count,amount,created_utc FROM shop_trades WHERE player=$0 AND status='pending'",id).Select(r=>string.Join(" / ",r)));
 });
 public Task<Result> ExecuteAsync(string actor,string action,string target,string value,GridCoord grid)=>m_Gate.RunAsync(async()=>
 {
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);var town=m_Cache.GetTownByName(target);
  if(action=="delete") {if(town==null)return Result.Fail("town_not_found");TownManagementService.DeleteTown(s,town.Id);}
  else if(action=="balance"||action=="addbalance"||action=="removebalance"||action=="townbalance"||action=="addtownbalance"||action=="removetownbalance")
  {
   if(!long.TryParse(value,out var amount)||amount<0)return Result.Fail("invalid_amount");
   var istown=action.Contains("town");var table=istown?"towns":"players";var column=istown?"bank_balance":"balance";var key=istown?"id":"steam64";
   long id;if(istown){if(town==null)return Result.Fail("town_not_found");id=town.Id.Value;}else if(!long.TryParse(target,out id)||id<=0)return Result.Fail("invalid_player");
   var old=s.Scalar($"SELECT {column} FROM {table} WHERE {key}=$0",id);if(old==null)return Result.Fail("invalid_target");
   var updated=action.StartsWith("add")?checked(Convert.ToInt64(old)+amount):action.StartsWith("remove")?checked(Convert.ToInt64(old)-amount):amount;
   if(updated<0)return Result.Fail("insufficient_balance");s.Execute($"UPDATE {table} SET {column}=$0 WHERE {key}=$1",updated,id);
  }
  else if(action=="claim")
  {
   if(town==null)return Result.Fail("town_not_found");if(m_Cache.GetClaim(grid)!=null)return Result.Fail("claim_already_owned");
   s.Execute("INSERT INTO claims(town_id,map_id,grid_x,grid_z) VALUES($0,$1,$2,$3)",town.Id.Value,grid.MapId,grid.X,grid.Z);
  }
  else if(action=="unclaim")
  {
   var claim=m_Cache.GetClaim(grid);if(claim==null)return Result.Fail("plot_not_found");
   s.Execute("DELETE FROM claims WHERE id=$0",claim.Id.Value);s.Execute("UPDATE towns SET spawn_x=NULL,spawn_y=NULL,spawn_z=NULL,spawn_yaw=NULL,spawn_map=NULL WHERE id=$0",claim.TownId.Value);
  }
  else if(action=="addmember")
  {
   if(town==null||!long.TryParse(value,out var player))return Result.Fail("invalid_target");
   if(s.Number("SELECT COUNT(*) FROM players WHERE steam64=$0",player)==0||s.Number("SELECT COUNT(*) FROM town_members WHERE player_steam64=$0",player)!=0)return Result.Fail("invalid_target");
   s.Execute("INSERT INTO town_members VALUES($0,$1,0,$2,0)",town.Id.Value,player,DateTime.UtcNow.ToString("O"));
  }
  else if(action=="removemember")
  {
   if(!ulong.TryParse(target,out var player))return Result.Fail("invalid_player");var member=m_Cache.GetMembership(new(player));if(member==null)return Result.Fail("not_in_town");if(member.Role==TownRole.Mayor)return Result.Fail("mayor_cannot_leave");TownManagementService.RemoveMember(s,new(player));
  }
  else if(action=="resolve")
  {
   if(value!="completed"&&value!="cancelled")return Result.Fail("syntax");
   var trades=s.Rows("SELECT player,kind,amount FROM shop_trades WHERE id=$0 AND status='pending'",target);
   if(trades.Count!=1)return Result.Fail("invalid_target");
   var trade=trades[0];
   if(((string)trade[1]! == "sell"&&value=="completed")||((string)trade[1]! == "buy"&&value=="cancelled"))
    if(s.Execute("UPDATE players SET balance=balance+$0 WHERE steam64=$1 AND balance<=9223372036854775807-$0",trade[2],trade[0])!=1)return Result.Fail("invalid_amount");
   s.Execute("UPDATE shop_trades SET status=$0 WHERE id=$1 AND status='pending'",value,target);
  }
  else if(action=="role")
  {
   if(!ulong.TryParse(target,out var player)||!Enum.TryParse<TownRole>(value,true,out var role)||!Enum.IsDefined(typeof(TownRole),role))return Result.Fail("invalid_target");
   var member=m_Cache.GetMembership(new(player));if(member==null)return Result.Fail("not_in_town");
   if(member.Role==TownRole.Mayor)return Result.Fail("mayor_cannot_leave");
   if(role==TownRole.Mayor){s.Execute("UPDATE town_members SET role=1 WHERE town_id=$0 AND role=2",member.TownId.Value);s.Execute("UPDATE towns SET mayor_steam64=$0 WHERE id=$1",(long)player,member.TownId.Value);}
   s.Execute("UPDATE town_members SET role=$0 WHERE player_steam64=$1",(int)role,(long)player);
  }
  else if(action=="nationremove")
  {
   if(town==null)return Result.Fail("town_not_found");
   s.Execute("DELETE FROM nations WHERE capital_town_id=$0",town.Id.Value);s.Execute("DELETE FROM nation_members WHERE town_id=$0",town.Id.Value);
  }
  else if(action=="endwar")
  {if(!long.TryParse(target,out var war))return Result.Fail("invalid_target");if(s.Execute("UPDATE wars SET status=4 WHERE id=$0 AND status IN (0,1,2)",war)!=1)return Result.Fail("invalid_target");}
  else return Result.Fail("syntax");
  s.Commit();await m_Cache.RebuildAsync();await m_Wars.RefreshAsync();m_Log.LogWarning("Admin {Actor}: {Action} target {Target} value {Value}",actor,action,target,value);return Result.Ok();
 });
}
