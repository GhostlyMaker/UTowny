using System.Collections.Concurrent;using Cysharp.Threading.Tasks;using Microsoft.Extensions.Options;using Microsoft.Extensions.Localization;using Microsoft.Extensions.Logging;using OpenMod.API.Eventing;using OpenMod.API.Permissions;using OpenMod.Unturned.Users;using OpenMod.Unturned.Players.Input.Events;using SDG.Unturned;using Steamworks;using UnityEngine;using UTowny.Configuration;using UTowny.Services;using UTowny.Domain.Common;
namespace UTowny.Visualization;
public sealed class ClaimToolService
{
 private readonly IGridService m_Grid;private readonly IOptions<UTownyOptions> m_Options;private readonly ConcurrentDictionary<ulong,DateTime> m_Last=new();private readonly ConcurrentDictionary<ulong,DateTime> m_Visible=new();
 public ClaimToolService(IGridService grid,IOptions<UTownyOptions> options){m_Grid=grid;m_Options=options;}
 public async Task<Result> ShowAsync(UnturnedUser user)
 {
  await UniTask.SwitchToMainThread();var id=user.Player.SteamId.m_SteamID;var now=DateTime.UtcNow;
  if(m_Last.TryGetValue(id,out var last)&&last.AddSeconds(3)>now)return Result.Fail("tool_cooldown");
  m_Last[id]=now;var effect=m_Options.Value.Visualization.EffectAssetId;if(effect==0||Assets.find(EAssetType.EFFECT,effect) is not EffectAsset asset)return Result.Fail("effect_missing");
  var p=user.Player.Player.transform.position;var grid=m_Grid.FromWorld(Level.info.name,p.x,p.z);var bounds=m_Grid.Bounds(grid);
  // Sixteen client-only markers, no saved world objects. The next scheduler tick clears expired effects.
  EffectManager.askEffectClearByID(effect,user.Player.SteamId);
  for(int edge=0;edge<4;edge++)for(int step=0;step<4;step++)
  {
   var t=step/4f;float x,z;
   if(edge==0){x=Mathf.Lerp(bounds.MinX,bounds.MaxX,t);z=bounds.MinZ;}else if(edge==1){x=bounds.MaxX;z=Mathf.Lerp(bounds.MinZ,bounds.MaxZ,t);}else if(edge==2){x=Mathf.Lerp(bounds.MaxX,bounds.MinX,t);z=bounds.MaxZ;}else{x=bounds.MinX;z=Mathf.Lerp(bounds.MaxZ,bounds.MinZ,t);}
   var point=new Vector3(x,p.y+0.2f,z);if(Physics.Raycast(new Vector3(x,p.y+128,z),Vector3.down,out var hit,256))point.y=hit.point.y+0.2f;
   Send(asset,user,point);
  }
  m_Visible[id]=now.AddSeconds(m_Options.Value.Visualization.DurationSeconds);return Result.Ok("claim_shown");
 }

 private static void Send(EffectAsset asset,UnturnedUser user,Vector3 point)
 {
  var parameters=new TriggerEffectParameters(asset){position=point,direction=Vector3.up,reliable=true,relevantPlayerID=user.Player.SteamId};
  EffectManager.triggerEffect(parameters);
 }
 public async Task<string> TestEffectAsync(UnturnedUser user,ushort id)
 {
  await UniTask.SwitchToMainThread();
  if(id==0||Assets.find(EAssetType.EFFECT,id) is not EffectAsset asset)return $"Effect {id} is not loaded on this server. Check its Type Effect and ID in the asset file.";
  var aim=user.Player.Player.look.aim;
  Send(asset,user,aim.position+aim.forward*3f);
  return $"Effect {id} sent 3 metres in front of you (asset lifetime: {asset.lifetime:0.##} seconds). If nothing appears, it may be audio-only, very brief, or unavailable on the client. This test does not change your configured boundary effect.";
 }
 public async Task ClearExpiredAsync(bool all=false,CancellationToken token=default)
 {
  if(m_Visible.IsEmpty)return;
  await UTowny.Utilities.UnityDispatch.RunAsync(()=>{foreach(var pair in m_Visible.ToArray())if(all||pair.Value<=DateTime.UtcNow){EffectManager.askEffectClearByID(m_Options.Value.Visualization.EffectAssetId,new CSteamID(pair.Key));m_Visible.TryRemove(pair.Key,out _);}},token).ConfigureAwait(false);
 }
}
public sealed class ClaimToolListener:IEventListener<UnturnedPlayerPluginKeyStateChangedEvent>
{
 private readonly BackgroundQueue m_Queue;
 private readonly IOptions<UTownyOptions> m_Options;private readonly IUnturnedUserDirectory m_Users;private readonly IPermissionChecker m_Permissions;private readonly IClaimService m_Claims;private readonly IGridService m_Grid;private readonly ClaimToolService m_Tool;private readonly IStringLocalizer m_Text;private readonly ILogger<ClaimToolListener> m_Log;
 public ClaimToolListener(IOptions<UTownyOptions> options,IUnturnedUserDirectory users,IPermissionChecker permissions,IClaimService claims,IGridService grid,ClaimToolService tool,IStringLocalizer text,ILogger<ClaimToolListener> log,BackgroundQueue queue){m_Queue=queue;m_Options=options;m_Users=users;m_Permissions=permissions;m_Claims=claims;m_Grid=grid;m_Tool=tool;m_Text=text;m_Log=log;}
 public Task HandleEventAsync(object? sender,UnturnedPlayerPluginKeyStateChangedEvent e)
 {
  if(e.State)m_Queue.Enqueue(()=>ExecuteAsync(e));return Task.CompletedTask;
 }
 private async Task ExecuteAsync(UnturnedPlayerPluginKeyStateChangedEvent e)
 {
  await UniTask.SwitchToMainThread();
  if(!e.State)return;var o=m_Options.Value.Visualization;
  if(o.ClaimToolAssetId==0||e.Player.Player.equipment.asset?.id!=o.ClaimToolAssetId)return;
  try
  {
   var user=m_Users.GetUser(e.Player.Player);if(await m_Permissions.CheckPermissionAsync(user,"UTowny:commands.town.claim")!=PermissionGrantResult.Grant)return;
   if(e.Key==o.PreviewKey){var shown=await m_Tool.ShowAsync(user);await UTowny.Utilities.UTownyChat.SendAsync(user,m_Text[shown.Code]);}
   else if(e.Key==o.ClaimKey){await UniTask.SwitchToMainThread();var p=e.Player.Player.transform.position;var grid=m_Grid.FromWorld(Level.info.name,p.x,p.z);var result=await m_Claims.ClaimAsync(new(e.Player.SteamId.m_SteamID),grid);await UTowny.Utilities.UTownyChat.SendAsync(user,m_Text[result.Code]);}
  }
  catch(Exception ex){m_Log.LogError(ex,"Claim tool operation failed");}
 }
}
