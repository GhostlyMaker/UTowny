using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OpenMod.API.Permissions;
using OpenMod.Unturned.Plugins;
using UTowny.Caching;
using UTowny.Persistence.Database;
using UTowny.Services;

[assembly: OpenMod.API.Plugins.PluginMetadata("UTowny", DisplayName = "UTowny")]
namespace UTowny.Plugin;

public sealed class UTownyPlugin : OpenModUnturnedPlugin
{
    private readonly ISchemaMigrator m_Migrator;
    private readonly IWorldStateCache m_Cache;
    private readonly IPlaytimeService m_Playtime;
    private readonly IPermissionRegistry m_Permissions;
    private readonly ILogger<UTownyPlugin> m_Logger;

    public UTownyPlugin(IServiceProvider serviceProvider, ISchemaMigrator migrator, IWorldStateCache cache,
        IPlaytimeService playtime, IPermissionRegistry permissions, ILogger<UTownyPlugin> logger) : base(serviceProvider)
    {
        m_Migrator = migrator;
        m_Cache = cache;
        m_Playtime = playtime;
        m_Permissions = permissions;
        m_Logger = logger;
    }

    protected override async UniTask OnLoadAsync()
    {
        await m_Migrator.MigrateAsync();
        await m_Cache.RebuildAsync();
        m_Permissions.RegisterPermission(this, "utowny.admin", "UTowny administrative commands");
        m_Permissions.RegisterPermission(this, "utowny.admin.bypass", "Bypass UTowny land restrictions");
        m_Logger.LogInformation("UTowny loaded and persistent caches rebuilt.");
    }

    protected override async UniTask OnUnloadAsync()
    {
        await m_Playtime.FlushAllAsync();
        m_Logger.LogInformation("UTowny unloaded cleanly.");
    }
}
