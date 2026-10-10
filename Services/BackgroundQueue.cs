using Microsoft.Extensions.Logging;
namespace UTowny.Services;
// OpenMod dispatches Unturned events synchronously. Queue persistence work so the
// game's event thread never waits for a database operation that may need Unity.
public sealed class BackgroundQueue
{
 private readonly object m_Lock=new();private Task m_Tail=Task.CompletedTask;private bool m_Closed;
 private readonly ILogger<BackgroundQueue> m_Log;
 public BackgroundQueue(ILogger<BackgroundQueue> log)=>m_Log=log;
 public void Enqueue(Func<Task> work)
 {
  lock(m_Lock)
  {
   if(m_Closed)return;
   m_Tail=m_Tail.ContinueWith(async _=>{try{await work();}catch(Exception ex){m_Log.LogError(ex,"UTowny queued event failed");}},CancellationToken.None,TaskContinuationOptions.None,TaskScheduler.Default).Unwrap();
  }
 }
 public Task DrainAsync(){lock(m_Lock){m_Closed=true;return m_Tail;}}
}
