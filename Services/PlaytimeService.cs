using System.Diagnostics;using Microsoft.Extensions.Options;using UTowny.Configuration;using UTowny.Domain.Common;using UTowny.Persistence.Repositories;
namespace UTowny.Services;
public interface IPlaytimeService
{
 Task PlayerConnectedAsync(PlayerId p,CancellationToken ct=default);Task PlayerDisconnectedAsync(PlayerId p,CancellationToken ct=default);Task<long> GetTotalSecondsAsync(PlayerId p,CancellationToken ct=default);Task FlushAllAsync(CancellationToken ct=default);Task CheckpointAsync(CancellationToken ct=default);
}
public sealed class PlaytimeService:IPlaytimeService
{
 private readonly IPlayerRepository m_Players;private readonly IOptions<UTownyOptions> m_Options;private readonly MutationGate m_Gate;private readonly Dictionary<ulong,long> m_Sessions=new();
 public PlaytimeService(IPlayerRepository players,IOptions<UTownyOptions> options,MutationGate gate){m_Players=players;m_Options=options;m_Gate=gate;}
 public async Task PlayerConnectedAsync(PlayerId p,CancellationToken ct=default)=>await m_Gate.RunAsync(async()=>{await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);if(!m_Sessions.ContainsKey(p.Value))m_Sessions[p.Value]=Stopwatch.GetTimestamp();return true;},ct);
 public async Task PlayerDisconnectedAsync(PlayerId p,CancellationToken ct=default)=>await m_Gate.RunAsync(async()=>{await Flush(p,ct);m_Sessions.Remove(p.Value);return true;},ct);
 private async Task Flush(PlayerId p,CancellationToken ct)
 {if(!m_Sessions.TryGetValue(p.Value,out var start))return;var now=Stopwatch.GetTimestamp();var seconds=(now-start)/Stopwatch.Frequency;if(seconds>0){await m_Players.AddPlaytimeAsync(p,seconds,DateTime.UtcNow,ct);m_Sessions[p.Value]=start+seconds*Stopwatch.Frequency;}}
 public Task<long> GetTotalSecondsAsync(PlayerId p,CancellationToken ct=default)=>m_Gate.RunAsync(async()=>{var profile=await m_Players.EnsureAsync(p,m_Options.Value.Economy.StartingBalance,ct);return profile.PlaytimeSeconds+(m_Sessions.TryGetValue(p.Value,out var start)?(Stopwatch.GetTimestamp()-start)/Stopwatch.Frequency:0);},ct);
 public async Task CheckpointAsync(CancellationToken ct=default)=>await m_Gate.RunAsync(async()=>{foreach(var id in m_Sessions.Keys.ToArray())await Flush(new(id),ct);return true;},ct);
 public async Task FlushAllAsync(CancellationToken ct=default)=>await m_Gate.RunAsync(async()=>{foreach(var id in m_Sessions.Keys.ToArray())await Flush(new(id),ct);m_Sessions.Clear();return true;},ct);
}
