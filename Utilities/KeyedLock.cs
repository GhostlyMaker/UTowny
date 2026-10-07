using System.Collections.Concurrent;
namespace UTowny.Utilities;
public sealed class KeyedLock<TKey> where TKey:notnull
{
 private readonly ConcurrentDictionary<TKey,SemaphoreSlim> m_Locks=new();
 public async ValueTask<IDisposable> AcquireAsync(TKey key,CancellationToken ct=default){var sem=m_Locks.GetOrAdd(key,_=>new SemaphoreSlim(1,1));await sem.WaitAsync(ct);return new Releaser(sem);}
 private sealed class Releaser:IDisposable{private SemaphoreSlim? m_S;public Releaser(SemaphoreSlim s)=>m_S=s;public void Dispose()=>Interlocked.Exchange(ref m_S,null)?.Release();}
}
