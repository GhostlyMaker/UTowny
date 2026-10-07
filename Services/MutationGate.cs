namespace UTowny.Services;

// All domain writes use this plugin-scoped gate, including callers of the public API.
// SQLite async APIs are synchronous internally, so execute persistence off Unity's thread.
public sealed class MutationGate
{
    private readonly SemaphoreSlim m_Gate = new(1, 1);
    private readonly AsyncLocal<int> m_Depth = new();
    public async Task<T> RunAsync<T>(Func<Task<T>> operation, CancellationToken ct = default)
    {
        if (m_Depth.Value != 0) return await operation();
        await m_Gate.WaitAsync(ct);
        try { return await Task.Run(async () => { m_Depth.Value++; try { return await operation(); } finally { m_Depth.Value--; } }, ct); }
        finally { m_Gate.Release(); }
    }
}
