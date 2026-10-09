using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OpenMod.API.Eventing;
using OpenMod.API.Permissions;
using OpenMod.Unturned.Users;
using OpenMod.Unturned.Players.Input.Events;
using SDG.Unturned;
using Steamworks;
using UnityEngine;
using UTowny.Caching;
using UTowny.Configuration;
using UTowny.Services;
using UTowny.Domain.Common;
using UTowny.Domain.Claims;
using UTowny.Utilities;

namespace UTowny.Visualization;
public sealed class ClaimToolService
{
    private sealed class Preview
    {
        public ushort Effect;
        public EffectAsset Asset = null!;
        public UnturnedUser User = null!;
        public string MapId = "";
        public PreviewRefresh<Vector3> Refresh = null!;
    }
    private readonly IGridService m_Grid;
    private readonly IOptions<UTownyOptions> m_Options;
    private readonly IWorldStateCache m_Cache;
    private readonly IStringLocalizer m_Text;
    private readonly ConcurrentDictionary<ulong, DateTime> m_Last = new();
    private readonly ConcurrentDictionary<ulong, Preview> m_Visible = new();
    public CancellationToken Token { get; set; }
    public ClaimToolService(IGridService grid, IOptions<UTownyOptions> options, IWorldStateCache cache, IStringLocalizer text)
    { m_Grid = grid; m_Options = options; m_Cache = cache; m_Text = text; }

    private string Message(string key, string fallback, params object[] args)
    { var value = m_Text[key, args]; return value.ResourceNotFound ? fallback : value.Value; }

    public async Task<string> ShowAsync(UnturnedUser user, string mode = "town")
    {
        mode = mode.ToLowerInvariant();
        if (mode != "town" && mode != "cell" && mode != "off")
            return Message("grid_usage", "Usage: /t show [town|cell|off]. Town shows the outer boundary and every claimed cell.");
        var id = user.Player.SteamId.m_SteamID;
        GridPreview? geometry = null;
        Preview? preview = null;
        EffectAsset? asset = null;
        var originY = 0f;
        var mapId = "";
        var description = "";
        var size = m_Options.Value.Claims.GridSizeMeters;
        var options = m_Options.Value.Visualization;
        try
        {
            await UnityDispatch.RunAsync(() =>
            {
                if (mode == "off")
                {
                    Clear(id);
                    description = Message("grid_off", "Boundary preview stopped. Effect splatters may remain until their own lifetime ends.");
                    return;
                }
                var now = DateTime.UtcNow;
                if (m_Last.TryGetValue(id, out var last) && last.AddSeconds(3) > now)
                { description = m_Text["tool_cooldown"]; return; }
                m_Last[id] = now;
                if (options.EffectAssetId == 0 || Assets.find(EAssetType.EFFECT, options.EffectAssetId) is not EffectAsset found)
                { description = m_Text["effect_missing"]; return; }
                asset = found;
                var p = user.Player.Player.transform.position;
                originY = p.y;
                var current = m_Grid.FromWorld(Level.info.name, p.x, p.z);
                mapId = current.MapId;
                IEnumerable<GridCoord> cells = new[] { current };
                var label = "current cell";
                if (mode == "town")
                {
                    var townId = m_Cache.GetClaim(current)?.TownId ?? m_Cache.GetMembership(new PlayerId(id))?.TownId;
                    if (townId != null)
                    {
                        cells = m_Cache.GetTownClaims(townId.Value).Select(c => c.Grid);
                        label = m_Cache.GetTown(townId.Value)?.Name ?? "town";
                    }
                }
                try { geometry = ClaimGridGeometry.Build(cells, mapId, size, options.MarkerSpacingMeters, options.MaxMarkers); }
                catch (PreviewTooLargeException)
                {
                    description = Message("grid_too_large", "The full town grid exceeds the marker limit. Use /t show cell, or ask an admin to increase marker_spacing_meters or max_markers. No partial grid was drawn.");
                    return;
                }
                if (geometry.CellCount == 0)
                { description = Message("grid_empty", "Your town has no claimed cells on this map. Use /t show cell to preview the cell here."); return; }
                Clear(id);
                preview = new Preview { Effect = options.EffectAssetId, Asset = asset, User = user, MapId = mapId,
                    Refresh = new PreviewRefresh<Vector3>(TimeSpan.FromSeconds(options.RefreshSeconds)) };
                m_Visible[id] = preview;
                description = Message("grid_shown",
                    $"Showing {label}: {geometry.CellCount} cell(s), {size} x {size} metres each; outer boundary and internal grid, {geometry.Markers.Count} markers. Current cell: {current.X}, {current.Z}. /t show cell isolates this cell; /t show off stops the preview.",
                    new { Name = label, Cells = geometry.CellCount, Size = size, Markers = geometry.Markers.Count, X = current.X, Z = current.Z });
            }, Token).ConfigureAwait(false);
            if (preview == null || geometry == null || asset == null) return description;
            // Limit per-frame raycasts and reliable packets; cancellation must work during unload.
            for (int start = 0; start < geometry.Markers.Count; start += 64)
            {
                if (start != 0) await Task.Delay(25, Token).ConfigureAwait(false);
                var active = true;
                await UnityDispatch.RunAsync(() =>
                {
                    if (!m_Visible.TryGetValue(id, out var current) || !ReferenceEquals(current, preview))
                    { active = false; return; }
                    if (user.Player.Player == null || !Provider.clients.Any(c => c.player == user.Player.Player)
                        || m_Grid.FromWorld(Level.info.name, 0, 0).MapId != mapId)
                    { Clear(id); active = false; return; }
                    for (int i = start; i < Math.Min(start + 64, geometry.Markers.Count); i++)
                    {
                        var marker = geometry.Markers[i];
                        var point = new Vector3(marker.X, originY + options.MarkerHeightMeters, marker.Z);
                        // Terrain only: tree canopies, fences and roofs must not hide the grid.
                        if (Physics.Raycast(new Vector3(marker.X, originY + 2048f, marker.Z), Vector3.down,
                            out var hit, 4096f, RayMasks.GROUND, QueryTriggerInteraction.Ignore))
                            point.y = hit.point.y + options.MarkerHeightMeters;
                        Send(asset, user, point);
                        preview.Refresh.Add(point, DateTime.UtcNow);
                    }
                }, Token).ConfigureAwait(false);
                if (!active) return Message("grid_cancelled", "Boundary preview stopped.");
            }
            await UnityDispatch.RunAsync(() =>
            {
                if (m_Visible.TryGetValue(id, out var current) && ReferenceEquals(current, preview))
                    preview.Refresh.Complete(DateTime.UtcNow, TimeSpan.FromSeconds(options.DurationSeconds));
            }, Token).ConfigureAwait(false);
            return description;
        }
        catch (OperationCanceledException) when (Token.IsCancellationRequested)
        { return Message("grid_cancelled", "Boundary preview stopped."); }
        finally
        {
            // If rendering failed, let the scheduler/unload clear the partially sent effects.
            if (preview != null && preview.Refresh.Expires == DateTime.MaxValue)
            {
                // Cleanup is performed by the visual loop or unload on Unity's thread.
                // Volatile dictionary removal also prevents later refresh of this session.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try
                {
                    await UnityDispatch.RunAsync(() =>
                    {
                        if (m_Visible.TryGetValue(id, out var active) && ReferenceEquals(active, preview)) Clear(id);
                    }, cleanup.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { /* Unload retries cosmetic cleanup. */ }
            }
        }
    }

    private void Clear(ulong id)
    {
        if (m_Visible.TryRemove(id, out var preview))
        {
            preview.Refresh.Stop();
            EffectManager.askEffectClearByID(preview.Effect, new CSteamID(id));
        }
    }
    private static void Send(EffectAsset asset, UnturnedUser user, Vector3 point)
    {
        var parameters = new TriggerEffectParameters(asset) { position = point, direction = Vector3.up, reliable = true, relevantPlayerID = user.Player.SteamId };
        EffectManager.triggerEffect(parameters);
    }
    public async Task<string> TestEffectAsync(UnturnedUser user, ushort id)
    {
        var message = "";
        await UnityDispatch.RunAsync(() =>
        {
            if (id == 0 || Assets.find(EAssetType.EFFECT, id) is not EffectAsset asset)
            { message = $"Effect {id} is not loaded on this server. Check its Type Effect and ID in the asset file."; return; }
            var aim = user.Player.Player.look.aim;
            Send(asset, user, aim.position + aim.forward * 3f);
            message = $"Effect {id} sent 3 metres in front of you (asset lifetime: {asset.lifetime:0.##} seconds). Its particles and splatters control their own appearance and lifetime. This test does not change your configured boundary effect.";
        }, Token).ConfigureAwait(false);
        return message;
    }
    public async Task RefreshAsync(CancellationToken token)
    {
        if (m_Visible.IsEmpty) return;
        await UnityDispatch.RunAsync(() =>
        {
            foreach (var pair in m_Visible.ToArray())
            {
                var preview = pair.Value;
                var now = DateTime.UtcNow;
                if (preview.Refresh.IsExpired(now) || preview.User.Player.Player == null
                    || !Provider.clients.Any(c => c.player == preview.User.Player.Player)
                    || m_Grid.FromWorld(Level.info.name, 0, 0).MapId != preview.MapId)
                { Clear(pair.Key); continue; }
                // Reuse terrain positions, never repeat raycasts during refresh.
                foreach (var point in preview.Refresh.TakeDue(now, 512))
                    Send(preview.Asset, preview.User, point);
            }
        }, token).ConfigureAwait(false);
    }
    public async Task ClearExpiredAsync(bool all = false, CancellationToken token = default)
    {
        if (m_Visible.IsEmpty) return;
        await UnityDispatch.RunAsync(() =>
        {
            foreach (var pair in m_Visible.ToArray())
                if (all || pair.Value.Refresh.IsExpired(DateTime.UtcNow)) Clear(pair.Key);
        }, token).ConfigureAwait(false);
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
   if(e.Key==o.PreviewKey){var shown=await m_Tool.ShowAsync(user);await UTowny.Utilities.UTownyChat.SendAsync(user,shown);}
   else if(e.Key==o.ClaimKey){await UniTask.SwitchToMainThread();var p=e.Player.Player.transform.position;var grid=m_Grid.FromWorld(Level.info.name,p.x,p.z);var result=await m_Claims.ClaimAsync(new(e.Player.SteamId.m_SteamID),grid);await UTowny.Utilities.UTownyChat.SendAsync(user,m_Text[result.Code]);}
  }
  catch(Exception ex){m_Log.LogError(ex,"Claim tool operation failed");}
 }
}

