using HarmonyLib;using SDG.Unturned;using UnityEngine;using UTowny.Domain.Common;using UTowny.Services;
namespace UTowny.Protection;
// These RPC entry points were checked against OpenMod.Unturned.Redist 3.26.1.1.
// Harmony needs a static prefix; the delegate is scoped to the plugin's load/unload lifetime.
public sealed class InteractionAdapter : IDisposable
{
 private readonly IProtectionService m_Protection;private readonly IGridService m_Grid;
 private readonly Harmony m_Harmony=new("UTowny.interactions");private static Func<Component,Player,bool>? s_Allow;
 public InteractionAdapter(IProtectionService protection,IGridService grid){m_Protection=protection;m_Grid=grid;}
 public void Start()
 {
  s_Allow=(component,player)=>{var p=component.transform.position;return m_Protection.Can(new PlayerId(player.channel.owner.playerID.steamID.m_SteamID),m_Grid.FromWorld(Level.info.name,p.x,p.z),LandAction.Interact);};
  var names=new Dictionary<Type,string[]>{
   [typeof(InteractableDoor)]=new[]{"ReceiveToggleRequest"},[typeof(InteractableGenerator)]=new[]{"ReceiveToggleRequest"},
   [typeof(InteractableFire)]=new[]{"ReceiveToggleRequest"},[typeof(InteractableOven)]=new[]{"ReceiveToggleRequest"},
   [typeof(InteractableOxygenator)]=new[]{"ReceiveToggleRequest"},[typeof(InteractableSafezone)]=new[]{"ReceiveToggleRequest"},
   [typeof(InteractableSpot)]=new[]{"ReceiveToggleRequest"},[typeof(InteractableStereo)]=new[]{"ReceiveTrackRequest","ReceiveChangeVolumeRequest"},
   [typeof(InteractableStorage)]=new[]{"ReceiveRotDisplayRequest"},[typeof(InteractableMannequin)]=new[]{"ReceivePoseRequest","ReceiveUpdateRequest"},
   [typeof(InteractableLibrary)]=new[]{"ReceiveTransferLibraryRequest"},[typeof(InteractableBed)]=new[]{"ReceiveClaimRequest"}};
  try{foreach(var item in names)foreach(var name in item.Value){var method=AccessTools.Method(item.Key,name)??throw new MissingMethodException(item.Key.FullName,name);m_Harmony.Patch(method,prefix:new HarmonyMethod(typeof(InteractionAdapter),nameof(Prefix)));}}
  catch{Dispose();throw;}
 }
 private static bool Prefix(Component __instance,ref ServerInvocationContext context)
 {var player=context.GetPlayer();return player!=null&&s_Allow?.Invoke(__instance,player)==true;}
 public void Dispose(){m_Harmony.UnpatchAll("UTowny.interactions");s_Allow=null;}
}
