using Autofac;
using OpenMod.API;
using OpenMod.API.Permissions;
using OpenMod.API.Persistence;
using OpenMod.Core.Permissions;
using UTowny.Commands;

internal static class PermissionRegression
{
    public static void Run()
    {
        if (CommandPermissions.Resolve("town", "new") != "UTowny:commands.town.create" || CommandPermissions.NormalizeAction("town", "NEW") != "create") throw new Exception("Town creation alias changed permission");
        if (CommandPermissions.Resolve("town", "help") != "UTowny:commands.town.info") throw new Exception("Help permission is unregistered");
        var registry = new PermissionRegistry();
        var plugin = new Component();
        if (registry.FindPermission("UTowny:commands.town.info") != null) throw new Exception("Unexpected pre-existing permission");
        CommandPermissions.Register(registry, plugin);
        // Exercise the reported commands and every documented gameplay action.
        var commands = new Dictionary<string, string>
        {
            ["town"] = "create info list residents invite accept leave kick promote demote mayor disband deposit claim unclaim show pvp tax setspawn spawn public protection",
            ["plot"] = "info buy forsale notforsale permissions",
            ["nation"] = "create info members allies invite accept kick leave disband ally allyaccept allydecline unally",
            ["war"] = "request accept decline cancel info list"
        };
        foreach (var group in commands)
        {
            foreach (var action in group.Value.Split(' '))
            {
                var expected = $"UTowny:commands.{group.Key}.{action}";
                if (CommandPermissions.Resolve(group.Key, action) != expected) throw new Exception("Wrong permission: " + expected);
                var registration = registry.FindPermission(expected) ?? throw new Exception("Permission not registered: " + expected);
                if (registration.DefaultGrant != PermissionGrantResult.Default) throw new Exception("Registration must not grant access by default");
            }
            if (CommandPermissions.Resolve(group.Key, "typo") != null) throw new Exception("Unknown command must return syntax without checking an unregistered permission");
        }
        if (registry.FindPermission("UTowny:admin.bypass") == null || CommandPermissions.Resolve("utowny", "reload") != "UTowny:admin")
            throw new Exception("Admin permissions missing");
        foreach (var root in new[] { "balance", "buy", "sell", "pay" })
            if (CommandPermissions.Resolve(root, "info") != "UTowny:commands." + root) throw new Exception("Economy root permission changed");
        CommandPermissions.Register(registry, plugin);
        if (registry.GetPermissions(plugin).Count != 48) throw new Exception("Permission registration is not idempotent");
        plugin.IsComponentAlive = false;
        if (registry.FindPermission("UTowny:commands.town.info") != null) throw new Exception("Unloaded owner still active");
        var reloaded = new Component();
        CommandPermissions.Register(registry, reloaded);
        if (registry.FindPermission("UTowny:commands.town.info")?.Owner != reloaded) throw new Exception("Reload did not restore permissions");
        Console.WriteLine("PASS: all 46 gameplay permissions registered without default grants, unknown actions rejected, admin/economy permissions preserved, and registrations survive reload.");
    }

    private sealed class Component : IOpenModComponent
    {
        public string OpenModComponentId => "UTowny";
        public string WorkingDirectory => "";
        public bool IsComponentAlive { get; set; } = true;
        public ILifetimeScope LifetimeScope => null!;
        public IDataStore? DataStore => null;
    }
}
