using System;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// A simple async-compatible lock using SemaphoreSlim.
/// Replaces NeoSmart.AsyncLock with built-in .NET primitives.
/// </summary>
public sealed class AsyncLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Asynchronously acquires the lock. Dispose the returned releaser to release the lock.
    /// </summary>
    public async Task<IDisposable> LockAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(_semaphore);
    }

    /// <summary>
    /// Synchronously acquires the lock. Dispose the returned releaser to release the lock.
    /// </summary>
    public IDisposable Lock()
    {
        _semaphore.Wait();
        return new Releaser(_semaphore);
    }

    private readonly struct Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose() => _semaphore.Release();
    }
}
