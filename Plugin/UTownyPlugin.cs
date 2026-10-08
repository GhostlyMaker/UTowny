using UTowny.Utilities;using Cysharp.Threading.Tasks;using Microsoft.Extensions.Logging;using Microsoft.Extensions.Localization;using Microsoft.Extensions.Options;
using OpenMod.API.Permissions;using OpenMod.Unturned.Plugins;using OpenMod.Unturned.Users;using Steamworks;
using UTowny.Caching;using UTowny.Configuration;using UTowny.Domain.Common;using UTowny.Persistence.Database;using UTowny.Services;using UTowny.Protection;
[assembly: OpenMod.API.Plugins.PluginMetadata("UTowny", DisplayName = "UTowny")]
namespace UTowny.Plugin;
public sealed class UTownyPlugin:OpenModUnturnedPlugin
{
 private readonly ISchemaMigrator m_Migrator;private readonly ExtendedSchema m_Extended;private readonly IWorldStateCache m_Cache;private readonly IPlaytimeService m_Playtime;private readonly ScheduledService m_Scheduled;private readonly IWarService m_Wars;private readonly ProtectionService m_Protection;private readonly InteractionAdapter m_Interactions;private readonly IUnturnedUserDirectory m_Users;private readonly IStringLocalizer m_Text;private readonly ILogger<UTownyPlugin> m_Log;private readonly TeleportService m_Teleport;private readonly IPermissionRegistry m_Permissions;private readonly MutationGate m_Gate;
 private readonly BackgroundQueue m_Queue;
 private readonly UTowny.Visualization.ClaimToolService m_Tool;
 public bool Ready=>m_Protection.Ready;
 private CancellationTokenSource? m_Stop;private Task? m_Loop;
 public UTownyPlugin(IServiceProvider sp,ISchemaMigrator migrator,ExtendedSchema extended,IWorldStateCache cache,IPlaytimeService playtime,ScheduledService scheduled,IWarService wars,ProtectionService protection,InteractionAdapter interactions,IUnturnedUserDirectory users,IStringLocalizer text,ILogger<UTownyPlugin> log,TeleportService teleport,IPermissionRegistry permissions,MutationGate gate,UTowny.Visualization.ClaimToolService tool,BackgroundQueue queue,DomainEventPublisher events):base(sp)
 {events.Initialize(this);m_Queue=queue;m_Tool=tool;m_Migrator=migrator;m_Extended=extended;m_Cache=cache;m_Playtime=playtime;m_Scheduled=scheduled;m_Wars=wars;m_Protection=protection;m_Interactions=interactions;m_Users=users;m_Text=text;m_Log=log;m_Teleport=teleport;m_Permissions=permissions;m_Gate=gate;}
 protected override async UniTask OnLoadAsync()
 {
  try
  {
  await Task.Run(async()=>{await m_Migrator.MigrateAsync();await m_Extended.InitializeAsync();await m_Cache.RebuildAsync();await m_Wars.RefreshAsync();});
  await Notify(await m_Scheduled.ProcessAsync());
  m_Stop=new CancellationTokenSource();m_Teleport.Token=m_Stop.Token;
  await UniTask.SwitchToMainThread();m_Interactions.Start();
  var online=m_Users.GetOnlineUsers().Select(u=>new PlayerId(u.Player.SteamId.m_SteamID)).ToArray();
  foreach(var id in online)await m_Playtime.PlayerConnectedAsync(id);
  m_Permissions.RegisterPermission(this,"admin","UTowny administrator commands");m_Permissions.RegisterPermission(this,"admin.bypass","Temporary protection bypass");
  m_Protection.Ready=true;m_Loop=Task.Run(()=>RunAsync(m_Stop.Token));m_Log.LogInformation("UTowny loaded; schema and persistent timers ready");
  }
  catch
  {
   m_Protection.Ready=false;m_Stop?.Cancel();await UniTask.SwitchToMainThread();m_Interactions.Dispose();throw;
  }
 }
 private async Task RunAsync(CancellationToken token)
 {
  var ticks=0;
  while(!token.IsCancellationRequested)
  {
   try{await Task.Delay(TimeSpan.FromSeconds(1),token).ConfigureAwait(false);await m_Tool.ClearExpiredAsync(token:token).ConfigureAwait(false);if(++ticks%10==0){await m_Playtime.CheckpointAsync(token).ConfigureAwait(false);await Notify(await m_Scheduled.ProcessAsync().ConfigureAwait(false),token).ConfigureAwait(false);}}
   catch(OperationCanceledException)when(token.IsCancellationRequested){break;}
   catch(Exception ex){m_Log.LogError(ex,"UTowny periodic processing failed; persistent deadlines will be retried");}
  }
 }
 private async Task Notify(IReadOnlyList<(PlayerId Player,string Key,long Amount)> notices,CancellationToken token=default)
 {
  foreach(var n in notices)
  {
   Task delivery=Task.CompletedTask;
   await UnityDispatch.RunAsync(()=>{var user=m_Users.FindUser(new CSteamID(n.Player.Value));if(user!=null)delivery=user.PrintMessageAsync(m_Text[n.Key,new {Amount=n.Amount}]);},token).ConfigureAwait(false);
   await delivery.ConfigureAwait(false);
  }
 }
 protected override ValueTask<bool> OnDispose()
 {
  m_Log.LogInformation("UTowny disposal started");
  return base.OnDispose();
 }
 protected override async UniTask OnUnloadAsync()
 {
  m_Log.LogInformation("UTowny unload: stopping periodic work");
  m_Protection.Ready=false;m_Stop?.Cancel();
  if(m_Loop!=null)await AwaitUnloadStep(m_Loop,"periodic work").ConfigureAwait(false);
  m_Log.LogInformation("UTowny unload: removing interaction patches");
  m_Interactions.Dispose();
  m_Log.LogInformation("UTowny unload: draining queued events");
  await AwaitUnloadStep(m_Queue.DrainAsync(),"queued events").ConfigureAwait(false);
  m_Log.LogInformation("UTowny unload: flushing playtime");
  await AwaitUnloadStep(m_Gate.CloseAsync(()=>m_Playtime.FlushAllAsync()),"playtime flush").ConfigureAwait(false);
  // Cosmetic cleanup must not indefinitely block disposal if Unity stops pumping.
  using(var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(5)))
  {
   try{await m_Tool.ClearExpiredAsync(true,cleanup.Token).ConfigureAwait(false);}
   catch(OperationCanceledException)when(cleanup.IsCancellationRequested){m_Log.LogWarning("UTowny unload: skipped visual cleanup because the game thread did not respond");}
  }
  m_Stop?.Dispose();m_Log.LogInformation("UTowny unloaded");
 }
 private async Task AwaitUnloadStep(Task work,string step)
 {
  while(await Task.WhenAny(work,Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false)!=work)
   m_Log.LogWarning("UTowny unload is still waiting for {Step}",step);
  await work.ConfigureAwait(false);
 }
}
