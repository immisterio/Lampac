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
        TimeSpan timeSpan;


        public SemaphorManager(string key)
        {
            timeSpan = TimeSpan.FromSeconds(40);
            semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        }

        public SemaphorManager(string key, TimeSpan timeSpan)
        {
            this.timeSpan = timeSpan;
            semaphore = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
        }


        public Task WaitAsync()
        {
            return semaphore.WaitAsync(timeSpan);
        }

        public Task WaitAsync(TimeSpan timeSpan)
        {
            return semaphore.WaitAsync(timeSpan);
        }

        public void Release()
        {
            try
            {
                semaphore.Release();
            }
            catch { }
        }


        async public Task Invoke(Action action)
        {
            try
            {
                await semaphore.WaitAsync(timeSpan);
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
                await semaphore.WaitAsync(timeSpan);
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
                await semaphore.WaitAsync(timeSpan);
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
                await semaphore.WaitAsync(timeSpan);
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
