using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services;

public class SemaphorManager
{
    #region static
    static readonly Serilog.ILogger Log = Serilog.Log.ForContext<SemaphorManager>();

    public static int Stat_ContSemaphoreLocks
        => _semaphoreLocks.Count;

    private static readonly ConcurrentDictionary<string, SemaphoreEntry> _semaphoreLocks = new();
    #endregion

    private readonly string key;
    private readonly SemaphoreEntry semaphore;
    private readonly CancellationToken cancellationToken;
    private readonly TimeSpan timeSpan;
    private bool lockAcquired;


    public SemaphorManager(string key, TimeSpan timeSpan)
    {
        this.key = key;
        this.timeSpan = timeSpan;
        cancellationToken = CancellationToken.None;

        semaphore = _semaphoreLocks.GetOrAdd(
            key,
            static _ => new SemaphoreEntry(new SemaphoreSlim(1, 1))
        );
    }

    public SemaphorManager(string key, CancellationToken cancellationToken)
    {
        this.key = key;
        timeSpan = TimeSpan.FromSeconds(30);
        this.cancellationToken = cancellationToken;

        semaphore = _semaphoreLocks.GetOrAdd(
            key,
            static _ => new SemaphoreEntry(new SemaphoreSlim(1, 1))
        );
    }


    public async Task<bool> WaitAsync()
    {
        lockAcquired = await semaphore.WaitAsync(timeSpan, cancellationToken).ConfigureAwait(false);
        return lockAcquired;
    }

    public void Release()
    {
        try
        {
            if (lockAcquired)
            {
                semaphore.Release();
                lockAcquired = false;
            }

            if (semaphore.CurrentCount == 1)
            {
                if (_semaphoreLocks.TryRemove(key, out var removed))
                    removed.Dispose();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_lnowtelx");
        }
    }


    #region SemaphoreEntry
    private sealed class SemaphoreEntry : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;

        public SemaphoreEntry(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public int CurrentCount
            => _semaphore.CurrentCount;

        public Task<bool> WaitAsync(TimeSpan timeSpan)
        {
            return _semaphore.WaitAsync(timeSpan);
        }

        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _semaphore.WaitAsync(cancellationToken);
        }

        public Task<bool> WaitAsync(TimeSpan timeSpan, CancellationToken cancellationToken)
        {
            return _semaphore.WaitAsync(timeSpan, cancellationToken);
        }

        public void Release()
        {
            _semaphore.Release();
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
    #endregion
}
