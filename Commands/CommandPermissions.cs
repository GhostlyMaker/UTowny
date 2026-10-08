using OpenMod.API;
using OpenMod.API.Permissions;

namespace UTowny.Commands;

// Routing and registration share the same set: never ask OpenMod to resolve
// an arbitrary permission name constructed from user input.
public static class CommandPermissions
{
    private static readonly IReadOnlyDictionary<string, string[]> Actions = new Dictionary<string, string[]>
    {
        ["town"] = new[] { "create", "info", "list", "residents", "invite", "accept", "leave", "kick", "promote", "demote", "mayor", "disband", "deposit", "claim", "unclaim", "show", "pvp", "tax", "setspawn", "spawn", "public", "protection" },
        ["plot"] = new[] { "info", "buy", "forsale", "notforsale", "permissions" },
        ["nation"] = new[] { "create", "info", "members", "allies", "invite", "accept", "kick", "leave", "disband", "ally", "allyaccept", "allydecline", "unally" },
        ["war"] = new[] { "request", "accept", "decline", "cancel", "info", "list" }
    };

    public static void Register(IPermissionRegistry registry, IOpenModComponent plugin)
    {
        foreach (var group in Actions)
            foreach (var action in group.Value)
                registry.RegisterPermission(plugin, $"commands.{group.Key}.{action}", $"UTowny {group.Key} {action}");
        registry.RegisterPermission(plugin, "admin", "UTowny administrator commands");
        registry.RegisterPermission(plugin, "admin.bypass", "Temporary protection bypass");
    }

    public static string NormalizeAction(string group, string action)
    {
        action = action.ToLowerInvariant();
        return group == "town" && action == "new" ? "create" : action;
    }

    public static string? Resolve(string group, string action)
    {
        action = NormalizeAction(group, action);
        if (action == "help") action = "info";
        if (group == "utowny") return "UTowny:admin";
        if (group == "balance" || group == "buy" || group == "sell") return "UTowny:commands." + group;
        return Actions.TryGetValue(group, out var actions) && actions.Contains(action)
            ? $"UTowny:commands.{group}.{action}"
            : null;
    }
}
