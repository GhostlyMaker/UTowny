using System.Collections.Concurrent;
using UTowny.Caching;
using UTowny.Domain.Claims;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Services;
namespace UTowny.Protection;
[Flags] public enum LandAction : long { Build=1,Damage=2,Salvage=4,Interact=8,Vehicle=16 }
public interface IProtectionService
{
 bool Can(PlayerId player,GridCoord grid,LandAction action);
 bool CanPvp(PlayerId attacker,PlayerId victim,GridCoord grid);
}
public sealed class ProtectionService : IProtectionService
{
 private readonly MutationGate m_Gate;
 private readonly IWorldStateCache m_Cache;private readonly IWarService m_Wars;
 private readonly ConcurrentDictionary<ulong,DateTime> m_Bypass=new();
 public bool Ready {get;set;}
 public ProtectionService(IWorldStateCache cache,IWarService wars,MutationGate gate){m_Gate=gate;m_Cache=cache;m_Wars=wars;}
 public void SetBypass(PlayerId player,bool enabled){if(enabled)m_Bypass[player.Value]=DateTime.UtcNow.AddMinutes(5);else m_Bypass.TryRemove(player.Value,out _);}
 public bool HasBypass(PlayerId player)=>m_Bypass.TryGetValue(player.Value,out var until)&&until>DateTime.UtcNow;
 public bool Can(PlayerId player,GridCoord grid,LandAction action)
 {
  if(!Ready||!m_Gate.Healthy)return false;
  if(HasBypass(player))return true;
  var claim=m_Cache.GetClaim(grid);if(claim==null)return true;
  var town=m_Cache.GetTown(claim.TownId);if(town==null)return false;
  var member=m_Cache.GetMembership(player);
  if(member?.TownId==claim.TownId && member.Role>=TownRole.CoMayor)return true;
  if(claim.PlotOwner==player)return true;
  var allowed=claim.PlotOwner!=null?claim.ProtectionFlags:town.ProtectionFlags;
  return (allowed&(long)action)!=0;
 }
 public bool CanPvp(PlayerId attacker,PlayerId victim,GridCoord grid)
 {
  if(!Ready||!m_Gate.Healthy)return false;
  if(attacker.Value==0||attacker==victim||HasBypass(attacker))return true;
  var a=m_Cache.GetMembership(attacker);var b=m_Cache.GetMembership(victim);
  if(a!=null&&b!=null&&m_Wars.AreTownsAtWar(a.TownId,b.TownId))return true;
  var claim=m_Cache.GetClaim(grid);return claim==null||m_Cache.GetTown(claim.TownId)?.PvpEnabled==true;
 }
}
