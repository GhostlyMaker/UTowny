using Cysharp.Threading.Tasks;using Microsoft.Extensions.Logging;using Microsoft.Extensions.Localization;using Microsoft.Extensions.Options;
using OpenMod.API.Permissions;using OpenMod.Unturned.Plugins;using OpenMod.Unturned.Users;using Steamworks;
using UTowny.Caching;using UTowny.Configuration;using UTowny.Domain.Common;using UTowny.Persistence.Database;using UTowny.Services;using UTowny.Protection;
[assembly: OpenMod.API.Plugins.PluginMetadata("UTowny", DisplayName = "UTowny")]
namespace UTowny.Plugin;
public sealed class UTownyPlugin:OpenModUnturnedPlugin
{
 private readonly ISchemaMigrator m_Migrator;private readonly ExtendedSchema m_Extended;private readonly IWorldStateCache m_Cache;private readonly IPlaytimeService m_Playtime;private readonly ScheduledService m_Scheduled;private readonly IWarService m_Wars;private readonly ProtectionService m_Protection;private readonly InteractionAdapter m_Interactions;private readonly IUnturnedUserDirectory m_Users;private readonly IStringLocalizer m_Text;private readonly ILogger<UTownyPlugin> m_Log;private readonly TeleportService m_Teleport;private readonly IPermissionRegistry m_Permissions;private readonly MutationGate m_Gate;
 private CancellationTokenSource? m_Stop;private Task? m_Loop;
 public UTownyPlugin(IServiceProvider sp,ISchemaMigrator migrator,ExtendedSchema extended,IWorldStateCache cache,IPlaytimeService playtime,ScheduledService scheduled,IWarService wars,ProtectionService protection,InteractionAdapter interactions,IUnturnedUserDirectory users,IStringLocalizer text,ILogger<UTownyPlugin> log,TeleportService teleport,IPermissionRegistry permissions,MutationGate gate):base(sp)
 {m_Migrator=migrator;m_Extended=extended;m_Cache=cache;m_Playtime=playtime;m_Scheduled=scheduled;m_Wars=wars;m_Protection=protection;m_Interactions=interactions;m_Users=users;m_Text=text;m_Log=log;m_Teleport=teleport;m_Permissions=permissions;m_Gate=gate;}
 protected override async UniTask OnLoadAsync()
 {
  await Task.Run(async()=>{await m_Migrator.MigrateAsync();await m_Extended.InitializeAsync();await m_Cache.RebuildAsync();await m_Wars.RefreshAsync();});
  await Notify(await m_Scheduled.ProcessAsync());
  m_Stop=new CancellationTokenSource();m_Teleport.Token=m_Stop.Token;
  await UniTask.SwitchToMainThread();m_Interactions.Start();
  var online=m_Users.GetOnlineUsers().Select(u=>new PlayerId(u.Player.SteamId.m_SteamID)).ToArray();
  foreach(var id in online)await m_Playtime.PlayerConnectedAsync(id);
  m_Permissions.RegisterPermission(this,"admin","UTowny administrator commands");m_Permissions.RegisterPermission(this,"admin.bypass","Temporary protection bypass");
  m_Protection.Ready=true;m_Loop=RunAsync(m_Stop.Token);m_Log.LogInformation("UTowny loaded; schema and persistent timers ready");
 }
 private async Task RunAsync(CancellationToken token)
 {
  while(!token.IsCancellationRequested)
  {
   try{await Task.Delay(TimeSpan.FromSeconds(10),token);await m_Playtime.CheckpointAsync(token);await Notify(await m_Scheduled.ProcessAsync());}
   catch(OperationCanceledException)when(token.IsCancellationRequested){break;}
   catch(Exception ex){m_Log.LogError(ex,"UTowny periodic processing failed; persistent deadlines will be retried");}
  }
 }
 private async Task Notify(IReadOnlyList<(PlayerId Player,string Key,long Amount)> notices)
 {
  await UniTask.SwitchToMainThread();
  foreach(var n in notices){var user=m_Users.FindUser(new CSteamID(n.Player.Value));if(user!=null)await user.PrintMessageAsync(m_Text[n.Key,new {Amount=n.Amount}]);}
 }
 protected override async UniTask OnUnloadAsync()
 {
  m_Protection.Ready=false;m_Stop?.Cancel();if(m_Loop!=null)await m_Loop;
  await UniTask.SwitchToMainThread();m_Interactions.Dispose();
  await m_Playtime.FlushAllAsync();m_Stop?.Dispose();m_Log.LogInformation("UTowny unloaded");
 }
}
