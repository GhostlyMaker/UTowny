using UTowny.Utilities;

internal static class DispatchRegression
{
    public static async Task RunAsync()
    {
        // Simulate Unity not pumping while unload cancels a pending timer action.
        Action? queued = null;
        var calls = 0;
        using var stop = new CancellationTokenSource();
        var pending = CancellableDispatch.RunAsync(a => queued = a, () => calls++, stop.Token);
        stop.Cancel();
        try { await pending.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { }
        queued!();
        if (calls != 0) throw new Exception("Cancelled work ran after unload");

        // Cancellation must not let unload run ahead of an action already executing.
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var runningStop = new CancellationTokenSource();
        Action? runningAction = null;
        var running = CancellableDispatch.RunAsync(a => runningAction = a, () =>
        {
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
        }, runningStop.Token);
        var pump = Task.Run(runningAction!);
        if (!entered.Wait(TimeSpan.FromSeconds(2))) throw new Exception("Action did not start");
        runningStop.Cancel();
        var abandoned = running.IsCompleted;
        release.Set();
        await Task.WhenAll(pump, running).WaitAsync(TimeSpan.FromSeconds(2));
        if (abandoned) throw new Exception("Running work was abandoned on cancellation");

        // Neither waiting nor finishing should depend on the caller's context.
        var previous = SynchronizationContext.Current;
        var context = new StoppedContext();
        Task task;
        using var contextStop = new CancellationTokenSource();
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            task = CancellableDispatch.RunAsync(_ => { }, () => { }, contextStop.Token);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        contextStop.Cancel();
        try { await task.WaitAsync(TimeSpan.FromSeconds(2)); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { }
        if (context.Posts != 0) throw new Exception("Dispatch captured the stopped context");
        Console.WriteLine("PASS: queued work cancels without Unity, stale callbacks do not run, running work drains, and no caller context is captured.");
    }

    private sealed class StoppedContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback callback, object? state) => Interlocked.Increment(ref Posts);
    }
}
