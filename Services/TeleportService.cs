using Microsoft.Extensions.Localization;using UTowny.Utilities;using System.Collections.Concurrent;using Cysharp.Threading.Tasks;using Microsoft.Extensions.Options;using OpenMod.Unturned.Users;using UTowny.Caching;using UTowny.Configuration;using UTowny.Domain.Common;using UnityEngine;using SDG.Unturned;
namespace UTowny.Services;
public sealed class TeleportService
{
 private readonly IStringLocalizer m_Text;
 private readonly IWorldStateCache m_Cache;private readonly IEconomyService m_Economy;private readonly IOptions<UTownyOptions> m_Options;private readonly IGridService m_Grid;
 private readonly ConcurrentDictionary<ulong,DateTime> m_Combat=new(),m_Cooldowns=new();private readonly ConcurrentDictionary<ulong,byte> m_Pending=new();
 public CancellationToken Token {get;set;}
 public TeleportService(IWorldStateCache cache,IEconomyService economy,IOptions<UTownyOptions> options,IGridService grid,IStringLocalizer text){m_Text=text;m_Cache=cache;m_Economy=economy;m_Options=options;m_Grid=grid;}
 public void Damaged(ulong player)=>m_Combat[player]=DateTime.UtcNow;
 public async Task<Result> SpawnAsync(UnturnedUser user,string? name)
 {
  var id=new PlayerId(user.Player.SteamId.m_SteamID);var o=m_Options.Value.Teleportation;
  if(!m_Pending.TryAdd(id.Value,0))return Result.Fail("teleport_pending");
  try
  {
   var member=m_Cache.GetMembership(id);var town=name==null?(member==null?null:m_Cache.GetTown(member.TownId)):m_Cache.GetTownByName(name);
   if(town?.Spawn==null)return Result.Fail("spawn_missing");
   if(member?.TownId!=town.Id&&!town.SpawnPublic)return Result.Fail("spawn_private");
   var started=DateTime.UtcNow;
   if(m_Cooldowns.TryGetValue(id.Value,out var next)&&next>started)return Result.Fail("teleport_cooldown");
   if(m_Combat.TryGetValue(id.Value,out var combat)&&combat.AddSeconds(o.CombatLockSeconds)>started)return Result.Fail("in_combat");
   await UniTask.SwitchToMainThread();var player=user.Player.Player;var origin=player.transform.position;
   if(player.life.isDead||player.movement.getVehicle()!=null||town.SpawnMap!=m_Grid.FromWorld(Level.info.name,0,0).MapId)return Result.Fail("spawn_unavailable");
   await CountdownAsync(user,o.WarmupSeconds);
   for(var elapsed=0;elapsed<o.WarmupSeconds*4;elapsed++)
   {
    await Task.Delay(250,Token);await UniTask.SwitchToMainThread();
    if(player==null||player.life.isDead||!Provider.clients.Any(c=>c.player==player))return Result.Fail("teleport_cancelled");
    if(o.CancelOnMovement&&(player.transform.position-origin).sqrMagnitude>0.25f)return Result.Fail("teleport_cancelled");
    if(o.CancelOnDamage&&m_Combat.TryGetValue(id.Value,out var hit)&&hit>=started)return Result.Fail("teleport_cancelled");
    if((elapsed+1)%4==0&&elapsed+1<o.WarmupSeconds*4)await CountdownAsync(user,o.WarmupSeconds-(elapsed+1)/4);
   }
   town=m_Cache.GetTown(town.Id);member=m_Cache.GetMembership(id);
   if(town?.Spawn==null||(member?.TownId!=town.Id&&!town.SpawnPublic))return Result.Fail("spawn_unavailable");
   var spawn=town.Spawn;
   if(m_Cache.GetClaim(m_Grid.FromWorld(town.SpawnMap!,spawn.X,spawn.Z))?.TownId!=town.Id)return Result.Fail("spawn_unavailable");
   if(o.Cost>0){var debit=await m_Economy.DebitAsync(id,o.Cost);if(!debit.Success)return Result.Fail(debit.Code);}
   await UniTask.SwitchToMainThread();
   bool moved=!Token.IsCancellationRequested&&player!=null&&!player.life.isDead&&Provider.clients.Any(c=>c.player==player)&&(!o.CancelOnMovement||(player.transform.position-origin).sqrMagnitude<=0.25f)&&(!o.CancelOnDamage||!m_Combat.TryGetValue(id.Value,out var finalHit)||finalHit<started)&&player.teleportToLocation(new Vector3(spawn.X,spawn.Y,spawn.Z),spawn.Yaw);
   if(!moved){if(o.Cost>0)await m_Economy.CreditAsync(id,o.Cost);return Result.Fail("teleport_cancelled");}
   m_Cooldowns[id.Value]=DateTime.UtcNow.AddSeconds(o.CooldownSeconds);return Result.Ok("teleported");
  }
  catch(OperationCanceledException){return Result.Fail("teleport_cancelled");}
  finally{m_Pending.TryRemove(id.Value,out _);}
 }
 private Task CountdownAsync(UnturnedUser user,int seconds)
 {
  var key=seconds>0?"teleport_countdown":"teleport_starting";
  var message=m_Text[key,new {Seconds=seconds}];
  return UTownyChat.SendAsync(user,message.ResourceNotFound?(seconds>0?$"Teleporting to town spawn in {seconds} seconds...":"Teleporting to town spawn now..."):message.Value);
 }

}
