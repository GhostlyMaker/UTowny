using Microsoft.Extensions.Options;
using UTowny.Configuration;
using UTowny.Domain.Common;
using UTowny.Persistence.Repositories;
using System.Collections.Concurrent;
namespace UTowny.Services;

public interface IPlaytimeService { Task PlayerConnectedAsync(PlayerId p,CancellationToken ct=default); Task PlayerDisconnectedAsync(PlayerId p,CancellationToken ct=default); Task<long> GetTotalSecondsAsync(PlayerId p,CancellationToken ct=default); Task FlushAllAsync(CancellationToken ct=default); }
public sealed class PlaytimeService:IPlaytimeService
{
 private readonly IPlayerRepository m_Players;private readonly IOptions<UTownyOptions> m_Options;private readonly ConcurrentDictionary<ulong,DateTime> m_Sessions=new();
 public PlaytimeService(IPlayerRepository players,IOptions<UTownyOptions> options){m_Players=players;m_Options=options;}
 public async Task PlayerConnectedAsync(PlayerId p,CancellationToken ct=default){await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);m_Sessions[p.Value]=DateTime.UtcNow;}
 public async Task PlayerDisconnectedAsync(PlayerId p,CancellationToken ct=default){if(!m_Sessions.TryRemove(p.Value,out var start))return;var now=DateTime.UtcNow;var sec=Math.Max(0,(long)(now-start).TotalSeconds);await m_Players.AddPlaytimeAsync(p,sec,now,ct);}
 public async Task<long> GetTotalSecondsAsync(PlayerId p,CancellationToken ct=default){var profile=await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);var live=m_Sessions.TryGetValue(p.Value,out var s)?Math.Max(0,(long)(DateTime.UtcNow-s).TotalSeconds):0;return profile.PlaytimeSeconds+live;}
 public async Task FlushAllAsync(CancellationToken ct=default){foreach(var p in m_Sessions.Keys.ToArray())await PlayerDisconnectedAsync(new(p),ct);}
}
