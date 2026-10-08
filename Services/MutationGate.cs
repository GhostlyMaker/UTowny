using UTowny.Caching;using UTowny.Api.Events;using UTowny.Domain.Common;
namespace UTowny.Services;

// All domain writes use this plugin-scoped gate, including callers of the public API.
// SQLite async APIs are synchronous internally, so execute persistence off Unity's thread.
public sealed class MutationGate
{
    private readonly IWorldStateCache m_Cache;
    private readonly DomainEventPublisher m_Events;
    public MutationGate(IWorldStateCache cache,DomainEventPublisher events){m_Cache=cache;m_Events=events;}
    private readonly SemaphoreSlim m_Gate = new(1, 1);
    private readonly AsyncLocal<int> m_Depth = new();
    public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken ct = default,DomainOperation? change=null)
    {
        if (m_Depth.Value != 0) return await operation();
        await m_Gate.WaitAsync(ct);
        try { return await Task.Run(async () => { m_Depth.Value++; m_Cache.BeginMutation(); var publish=false; try { if(change!=null)await m_Events.BeforeAsync(change);var result=await operation(); publish=true; if(change!=null && (result is not IOutcome outcome || outcome.Success))await m_Events.AfterAsync(change);return result; } finally { m_Cache.CompleteMutation(publish); m_Depth.Value--; } }, ct); }
        finally { m_Gate.Release(); }
    }
}
