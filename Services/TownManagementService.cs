using Microsoft.Extensions.Options;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Domain.Towns;
using UTowny.Persistence.Database;
namespace UTowny.Services;

public interface ITownManagementService
{
    Task<Result> InviteAsync(PlayerId actor, PlayerId target);
    Task<Result> AcceptAsync(PlayerId actor, TownId town);
    Task<Result> RemoveAsync(PlayerId actor, PlayerId target);
    Task<Result> RoleAsync(PlayerId actor, PlayerId target, TownRole role);
    Task<Result> DisbandAsync(PlayerId actor);
    Task<Result> SetAsync(PlayerId actor, string setting, long value);
    Task<Result> SetSpawnAsync(PlayerId actor, TownSpawn spawn, string map);
}
public sealed class TownManagementService : ITownManagementService
{
    private readonly IDatabaseConnectionFactory m_Db;
    private readonly IWorldStateCache m_Cache;
    private readonly IAuthorizationService m_Auth;
    private readonly MutationGate m_Gate;
    private readonly IGridService m_Grid;
    private readonly IOptions<UTownyOptions> m_Options;
    public TownManagementService(IDatabaseConnectionFactory db, IWorldStateCache cache, IAuthorizationService auth, MutationGate gate, IGridService grid, IOptions<UTownyOptions> options)
    { m_Db = db; m_Cache = cache; m_Auth = auth; m_Gate = gate; m_Grid = grid; m_Options = options; }
    private Task<Result> Change(PlayerId actor, TownAction action, Func<SqlSession, TownId, Result> body) => m_Gate.RunAsync(async () =>
    {
        var member = m_Cache.GetMembership(actor);
        if (member == null) return Result.Fail("not_in_town");
        if (!m_Auth.Can(actor, member.TownId, action)) return Result.Fail("insufficient_role");
        using var db = await m_Db.OpenAsync(); using var s = new SqlSession(db);
        var result = body(s, member.TownId);
        if (result.Success) { s.Commit(); await m_Cache.RebuildAsync(); }
        return result;
    },change:new UTowny.Api.Events.DomainOperation("town."+action.ToString().ToLowerInvariant(),actor));
    public Task<Result> InviteAsync(PlayerId actor, PlayerId target) => Change(actor, TownAction.Invite, (s, town) =>
    {
        if (s.Number("SELECT COUNT(*) FROM town_members WHERE player_steam64=$0", (long)target.Value) != 0) return Result.Fail("already_in_town");
        s.Execute("INSERT INTO town_invites VALUES($0,$1,$2,$3) ON CONFLICT(town_id,player_steam64) DO UPDATE SET invited_by_steam64=excluded.invited_by_steam64,expires_utc=excluded.expires_utc", town.Value, (long)target.Value, (long)actor.Value, DateTime.UtcNow.AddHours(24).ToString("O"));
        return Result.Ok("invited");
    });
    public Task<Result> AcceptAsync(PlayerId actor, TownId town) => m_Gate.RunAsync(async () =>
    {
        using var db = await m_Db.OpenAsync(); using var s = new SqlSession(db);
        if (s.Number("SELECT COUNT(*) FROM town_members WHERE player_steam64=$0", (long)actor.Value) != 0) return Result.Fail("already_in_town");
        if (s.Number("SELECT COUNT(*) FROM town_invites WHERE town_id=$0 AND player_steam64=$1 AND expires_utc>$2", town.Value, (long)actor.Value, DateTime.UtcNow.ToString("O")) == 0) return Result.Fail("invite_missing");
        s.Execute("INSERT OR IGNORE INTO players VALUES($0,$1,0,$2)", (long)actor.Value, m_Options.Value.Economy.StartingBalance, DateTime.UtcNow.ToString("O"));
        s.Execute("INSERT INTO town_members VALUES($0,$1,0,$2,0)", town.Value, (long)actor.Value, DateTime.UtcNow.ToString("O"));
        s.Execute("DELETE FROM town_invites WHERE player_steam64=$0", (long)actor.Value);
        s.Commit(); await m_Cache.RebuildAsync(); return Result.Ok("joined");
    },change:new UTowny.Api.Events.DomainOperation("town.join",actor,town));
    public static void RemoveMember(SqlSession s, PlayerId player)
    {
        s.Execute("UPDATE claims SET plot_owner_steam64=NULL,for_sale=0,price=0,protection_flags=0 WHERE plot_owner_steam64=$0", (long)player.Value);
        s.Execute("DELETE FROM town_members WHERE player_steam64=$0", (long)player.Value);
    }
    public Task<Result> RemoveAsync(PlayerId actor, PlayerId target) => Change(actor, TownAction.Kick, (s, town) =>
    {
        var member = m_Cache.GetMembership(target);
        var acting = m_Cache.GetMembership(actor)!;
        if (member == null || member.TownId != town) return Result.Fail("not_in_town");
        if (member.Role >= acting.Role || member.Role == TownRole.Mayor) return Result.Fail("insufficient_role");
        RemoveMember(s, target); return Result.Ok();
    });
    public Task<Result> RoleAsync(PlayerId actor, PlayerId target, TownRole role) => Change(actor, role == TownRole.Mayor ? TownAction.TransferMayor : TownAction.Promote, (s, town) =>
    {
        var member = m_Cache.GetMembership(target);
        if (member == null || member.TownId != town) return Result.Fail("not_in_town");
        if (m_Cache.GetMembership(actor)!.Role != TownRole.Mayor && role != TownRole.Mayor) return Result.Fail("not_mayor");
        if (member.Role == TownRole.Mayor) return Result.Fail("mayor_cannot_leave");
        if (role == TownRole.Mayor)
        {
            s.Execute("UPDATE town_members SET role=1 WHERE town_id=$0 AND role=2", town.Value);
            s.Execute("UPDATE towns SET mayor_steam64=$0 WHERE id=$1", (long)target.Value, town.Value);
        }
        s.Execute("UPDATE town_members SET role=$0 WHERE player_steam64=$1", (int)role, (long)target.Value); return Result.Ok();
    });
    public Task<Result> DisbandAsync(PlayerId actor) => Change(actor, TownAction.Disband, (s, town) => { DeleteTown(s, town); return Result.Ok("town_deleted"); });
    public static void DeleteTown(SqlSession s, TownId town)
    {
        s.Execute("DELETE FROM nations WHERE capital_town_id=$0", town.Value);
        s.Execute("DELETE FROM towns WHERE id=$0", town.Value);
    }
    public Task<Result> SetAsync(PlayerId actor, string setting, long value) => Change(actor, setting.StartsWith("tax") ? TownAction.SetTax : TownAction.SetPvp, (s, town) =>
    {
        var columns = new Dictionary<string, string> { ["pvp"]="pvp_enabled", ["tax"]="tax_enabled", ["taxamount"]="tax_amount", ["public"]="spawn_public", ["protection"]="protection_flags" };
        if (!columns.TryGetValue(setting, out var column) || value < 0 || (setting != "taxamount" && setting != "protection" && value > 1)) return Result.Fail("invalid_amount");
        if (m_Cache.GetMembership(actor)!.Role != TownRole.Mayor) return Result.Fail("not_mayor");
        s.Execute($"UPDATE towns SET {column}=$0 WHERE id=$1", value, town.Value); return Result.Ok();
    });
    public Task<Result> SetSpawnAsync(PlayerId actor, TownSpawn spawn, string map) => Change(actor, TownAction.SetSpawn, (s, town) =>
    {
        if (m_Cache.GetClaim(m_Grid.FromWorld(map, spawn.X, spawn.Z))?.TownId != town) return Result.Fail("spawn_outside");
        s.Execute("UPDATE towns SET spawn_x=$0,spawn_y=$1,spawn_z=$2,spawn_yaw=$3,spawn_map=$4 WHERE id=$5", spawn.X, spawn.Y, spawn.Z, spawn.Yaw, map, town.Value); return Result.Ok();
    });
}
