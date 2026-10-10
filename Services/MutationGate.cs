using UTowny.Caching;using UTowny.Api.Events;using UTowny.Domain.Common;
namespace UTowny.Services;
// Domain writes and cache publication are serialized, off the Unity thread.
public sealed class MutationGate
{
 private readonly IWorldStateCache m_Cache;private readonly DomainEventPublisher m_Events;
 private readonly SemaphoreSlim m_Gate=new(1,1);private readonly AsyncLocal<int> m_Depth=new();
 private volatile bool m_Closed;public bool Healthy {get;private set;}=true;
 public MutationGate(IWorldStateCache cache,DomainEventPublisher events){m_Cache=cache;m_Events=events;}
 public async Task<T> RunAsync<T>(Func<Task<T>> operation,CancellationToken ct=default,DomainOperation? change=null)
 {
  if(m_Depth.Value!=0)return await operation();
  if(m_Closed)throw new ObjectDisposedException("UTowny");
  await m_Gate.WaitAsync(ct);
  try
  {
   if(m_Closed)throw new ObjectDisposedException("UTowny");
   return await Task.Run(async()=>
   {
    m_Depth.Value++;m_Cache.BeginMutation();var publish=false;
    try
    {
     if(change!=null)await m_Events.BeforeAsync(change);
     var result=await operation();publish=true;
     if(change!=null&&(result is not IOutcome outcome||outcome.Success))await m_Events.AfterAsync(change);
     return result;
    }
    catch
    {
     // A commit may have succeeded before cache refresh failed. Recover the source of truth.
     try{await m_Cache.RebuildAsync();publish=true;}catch{Healthy=false;}
     throw;
    }
    finally{m_Cache.CompleteMutation(publish);m_Depth.Value--;}
   },ct);
  }
  finally{m_Gate.Release();}
 }
 public async Task CloseAsync(Func<Task> finalFlush)
 {
  m_Closed=true;await m_Gate.WaitAsync();
  try{await Task.Run(async()=>{m_Depth.Value++;try{await finalFlush();}finally{m_Depth.Value--;}});}
  finally{m_Gate.Release();}
 }
}
