using Cysharp.Threading.Tasks;
namespace UTowny.Utilities;

public static class UnityDispatch
{
    public static Task RunAsync(Action action, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (PlayerLoopHelper.IsMainThread)
        {
            action();
            return Task.CompletedTask;
        }
        return CancellableDispatch.RunAsync(callback => UniTask.Post(callback), action, token);
    }
}
