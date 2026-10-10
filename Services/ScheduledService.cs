using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Persistence.Database;
namespace UTowny.Services;
public sealed class ScheduledService
{
 private readonly DomainEventPublisher m_Events;private readonly Dictionary<long,DateTime> m_Warned=new();
 private readonly IDatabaseConnectionFactory m_Db;private readonly IWorldStateCache m_Cache;private readonly MutationGate m_Gate;private readonly IOptions<UTownyOptions> m_Options;private readonly IWarService m_Wars;private readonly ILogger<ScheduledService> m_Log;
 public ScheduledService(IDatabaseConnectionFactory db,IWorldStateCache cache,MutationGate gate,IOptions<UTownyOptions> options,IWarService wars,ILogger<ScheduledService> log,DomainEventPublisher events){m_Events=events;m_Db=db;m_Cache=cache;m_Gate=gate;m_Options=options;m_Wars=wars;m_Log=log;}
 public Task<IReadOnlyList<(PlayerId Player,string Key,long Amount)>> ProcessAsync()=>m_Gate.RunAsync(async()=>
 {
  var notices=new List<(PlayerId,string,long)>();var now=DateTime.UtcNow;var o=m_Options.Value;
  using var db=await m_Db.OpenAsync();using var s=new SqlSession(db);
  foreach(var town in m_Cache.GetTowns())
  {
   var tax=town.NextTaxUtc;var upkeep=town.NextUpkeepUtc;var deleted=false;
   // Bound each batch, retaining each original deadline; subsequent ticks continue overdue work.
   for(int cycle=0;cycle<256&&(tax<=now||upkeep<=now);cycle++)
   {
    if(tax<=upkeep&&tax<=now)
    {
     if(town.TaxEnabled&&town.TaxAmount>0)
     foreach(var row in s.Rows("SELECT player_steam64,role,missed_tax_cycles FROM town_members WHERE town_id=$0",town.Id.Value))
     {
      var player=new PlayerId((ulong)Convert.ToInt64(row[0]));var role=(TownRole)Convert.ToInt32(row[1]);
      if(role==TownRole.Mayor||o.Taxes.ExemptRoles.Contains(role.ToString(),StringComparer.OrdinalIgnoreCase))continue;
      if(s.Execute("UPDATE players SET balance=balance-$0 WHERE steam64=$1 AND balance>=$0",town.TaxAmount,(long)player.Value)==1)
      {
       if(s.Execute("UPDATE towns SET bank_balance=bank_balance+$0 WHERE id=$1 AND bank_balance<=9223372036854775807-$0",town.TaxAmount,town.Id.Value)!=1)throw new OverflowException("Town treasury overflow");
       s.Execute("UPDATE town_members SET missed_tax_cycles=0 WHERE player_steam64=$0",(long)player.Value);notices.Add((player,"tax_paid",town.TaxAmount));
      }
      else
      {
       var missed=Convert.ToInt32(row[2])+1;
       if(missed>=o.Taxes.MissedCyclesBeforeKick){TownManagementService.RemoveMember(s,player);notices.Add((player,"tax_removed",town.TaxAmount));}
       else {s.Execute("UPDATE town_members SET missed_tax_cycles=$0 WHERE player_steam64=$1",missed,(long)player.Value);notices.Add((player,"tax_failed",town.TaxAmount));}
      }
     }
     tax=tax.AddHours(o.Taxes.IntervalHours);s.Execute("UPDATE towns SET next_tax_utc=$0 WHERE id=$1",tax.ToString("O"),town.Id.Value);
    }
    else if(upkeep<=now)
    {
     var cost=checked(o.Upkeep.BaseAmount+o.Upkeep.PerClaimAmount*s.Number("SELECT COUNT(*) FROM claims WHERE town_id=$0",town.Id.Value)+o.Upkeep.PerResidentAmount*s.Number("SELECT COUNT(*) FROM town_members WHERE town_id=$0",town.Id.Value));
     if(s.Execute("UPDATE towns SET bank_balance=bank_balance-$0 WHERE id=$1 AND bank_balance>=$0",cost,town.Id.Value)!=1)
     {
      foreach(var row in s.Rows("SELECT player_steam64 FROM town_members WHERE town_id=$0",town.Id.Value))notices.Add((new PlayerId((ulong)Convert.ToInt64(row[0])),"town_deleted_upkeep",cost));
      TownManagementService.DeleteTown(s,town.Id);deleted=true;break;
     }
     notices.Add((town.MayorId,"upkeep_paid",cost));upkeep=upkeep.AddHours(o.Upkeep.IntervalHours);s.Execute("UPDATE towns SET next_upkeep_utc=$0 WHERE id=$1",upkeep.ToString("O"),town.Id.Value);
    }
   }
   if(deleted)m_Log.LogWarning("Town {TownId} {Town} deleted for unpaid upkeep",town.Id.Value,town.Name);
  }
  var oldWars=m_Wars.All.ToArray();
  s.Execute("UPDATE wars SET status=3 WHERE status IN (1,2) AND end_utc<=$0",now.ToString("O"));
  s.Execute("UPDATE wars SET status=2 WHERE status=1 AND start_utc<=$0 AND end_utc>$0",now.ToString("O"));
  s.Execute("DELETE FROM town_invites WHERE expires_utc<=$0; DELETE FROM nation_invites WHERE expires_utc<=$0",now.ToString("O"));
  s.Commit();await m_Cache.RebuildAsync();await m_Wars.RefreshAsync();
  foreach(var n in notices)await m_Events.AfterAsync(new UTowny.Api.Events.DomainOperation(n.Item2,n.Item1,Detail:n.Item3.ToString()));
  foreach(var war in m_Wars.All)
  {
   var old=oldWars.FirstOrDefault(w=>w.Id==war.Id);
   if(old?.Status==war.Status)continue;
   var key=war.Status==WarStatus.Active?"war_started":war.Status==WarStatus.Ended?"war_ended":null;
   if(key==null)continue;
   foreach(var member in m_Cache.GetMembers(war.A).Concat(m_Cache.GetMembers(war.B)))notices.Add((member.PlayerId,key,war.Id));
   await m_Events.AfterAsync(new UTowny.Api.Events.DomainOperation(key,new PlayerId(0),war.A,war.Id.ToString()));
  }
  foreach(var town in m_Cache.GetTowns())if(town.NextUpkeepUtc>now&&town.NextUpkeepUtc<=now.AddHours(1)&&(!m_Warned.TryGetValue(town.Id.Value,out var warned)||warned!=town.NextUpkeepUtc))
  {
   var amount=checked(o.Upkeep.BaseAmount+o.Upkeep.PerClaimAmount*m_Cache.GetTownClaims(town.Id).Count+o.Upkeep.PerResidentAmount*m_Cache.GetResidentCount(town.Id));
   notices.Add((town.MayorId,"upkeep_warning",amount));m_Warned[town.Id.Value]=town.NextUpkeepUtc;
  }
  return (IReadOnlyList<(PlayerId,string,long)>)notices;
 });
}
