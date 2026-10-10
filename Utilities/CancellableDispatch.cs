namespace UTowny.Utilities;

// Cancellation removes work that has not started, even if the target thread
// stops pumping. Once started, await completion rather than abandon the action.
public static class CancellableDispatch
{
    public static async Task RunAsync(Action<Action> post, Action action, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var state = 0; // queued, running, or cancelled
        using var registration = token.Register(() =>
        {
            if (Interlocked.CompareExchange(ref state, 2, 0) == 0)
                completion.TrySetCanceled(token);
        });
        post(() =>
        {
            if (Interlocked.CompareExchange(ref state, 1, 0) != 0) return;
            try { action(); completion.TrySetResult(true); }
            catch (Exception ex) { completion.TrySetException(ex); }
        });
        await completion.Task.ConfigureAwait(false);
    }
}
