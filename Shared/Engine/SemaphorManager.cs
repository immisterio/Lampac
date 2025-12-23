using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Engine
{
    public class SemaphorManager
    {
        #region static
        private static readonly ConcurrentDictionary<string, SemaphoreEntry> _semaphoreLocks = new();
        private static readonly Timer _cleanupTimer = new(_ => Cleanup(), null, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20));

        static void Cleanup()
        {
            var threshold = DateTime.UtcNow - TimeSpan.FromMinutes(2);

            foreach (var kvp in _semaphoreLocks.ToArray())
            {
                if (kvp.Value.LastUsed < threshold && _semaphoreLocks.TryRemove(kvp.Key, out var removed))
                    removed.Dispose();
            }
        }
        #endregion

        SemaphoreEntry semaphore { get; set; }
        CancellationToken cancellationToken;

        bool regwait, releaseLock;


        public SemaphorManager(string key)
        {
            cancellationToken = new CancellationTokenSource(TimeSpan.FromSeconds(40)).Token;
            semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        }

        public SemaphorManager(string key, TimeSpan timeSpan)
        {
            cancellationToken = new CancellationTokenSource(timeSpan).Token;
            semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        }

        public SemaphorManager(string key, CancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;
            semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        }


        public Task WaitAsync(TimeSpan timeSpan)
        {
            regwait = true;
            return semaphore.WaitAsync(timeSpan);
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            regwait = true;
            return semaphore.WaitAsync(cancellationToken);
        }

        public Task WaitAsync()
        {
            regwait = true;
            return semaphore.WaitAsync(cancellationToken);
        }


        public void Release()
        {
            try
            {
                if (regwait && releaseLock == false)
                {
                    releaseLock = true;
                    semaphore.Release();
                }
            }
            catch { }
        }


        async public Task Invoke(Action action)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
                action();
            }
            finally
            {
                semaphore.Release();
            }
        }

        async public Task Invoke(Func<Task> func)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
                await func();
            }
            finally
            {
                semaphore.Release();
            }
        }


        async public Task<T> Invoke<T>(Func<T> func)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
                return func();
            }
            finally
            {
                semaphore.Release();
            }
        }

        async public Task<T> Invoke<T>(Func<Task<T>> func)
        {
            try
            {
                await semaphore.WaitAsync(cancellationToken);
                return await func();
            }
            finally
            {
                semaphore.Release();
            }
        }


        #region SemaphoreEntry
        private sealed class SemaphoreEntry : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            public SemaphoreEntry(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
                Touch();
            }

            public DateTime LastUsed { get; private set; }

            public void Touch()
            {
                LastUsed = DateTime.UtcNow;
            }

            public Task WaitAsync(TimeSpan timeSpan)
            {
                Touch();
                return _semaphore.WaitAsync(timeSpan);
            }

            public Task WaitAsync(CancellationToken cancellationToken)
            {
                Touch();
                return _semaphore.WaitAsync(cancellationToken);
            }

            public void Release()
            {
                Touch();
                _semaphore.Release();
            }

            public void Dispose()
            {
                _semaphore.Dispose();
            }
        }
        #endregion
    }
}
