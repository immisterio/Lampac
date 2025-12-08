using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Engine
{
    public static class Semaphor
    {
        public static Task Invoke(string key, TimeSpan timeSpan, Func<Task> func) => SemaphorManager.Invoke(key, timeSpan, func);

        public static Task<T> Invoke<T>(string key, TimeSpan timeSpan, Func<Task<T>> func) => SemaphorManager.Invoke(key, timeSpan, func);
    }

    internal static class SemaphorManager
    {
        private static readonly ConcurrentDictionary<string, SemaphoreEntry> _semaphoreLocks = new();
        private static readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan _expiration = TimeSpan.FromMinutes(5);
        private static readonly Timer _cleanupTimer = new(_ => Cleanup(), null, _cleanupInterval, _cleanupInterval);

        public static async Task Invoke(string key, TimeSpan timeSpan, Func<Task> func)
        {
            var semaphore = GetSemaphoreEntry(key);

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

        public static async Task<T> Invoke<T>(string key, TimeSpan timeSpan, Func<Task<T>> func)
        {
            var semaphore = GetSemaphoreEntry(key);

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

        private static SemaphoreEntry GetSemaphoreEntry(string key)
        {
            var entry = _semaphoreLocks.GetOrAdd(key, _ => new SemaphoreEntry(new SemaphoreSlim(1, 1)));
            entry.Touch();
            return entry;
        }

        private static void Cleanup()
        {
            var threshold = DateTime.UtcNow - _expiration;

            foreach (var kvp in _semaphoreLocks)
            {
                if (kvp.Value.LastUsed < threshold && _semaphoreLocks.TryRemove(kvp.Key, out var removed))
                {
                    removed.Dispose();
                }
            }
        }

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
    }
}
