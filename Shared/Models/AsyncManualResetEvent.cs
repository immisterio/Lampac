using System.Threading;

namespace Shared.Models
{
    public class AsyncManualResetEvent
    {
        private volatile TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();

        public Task WaitAsync()
        {
            return tcs.Task;
        }

        async public ValueTask WaitAsync(int millisecondsTimeout)
        {
            try
            {
                await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(millisecondsTimeout));
            }
            catch { }
        }

        async public ValueTask WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            try
            {
                await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(millisecondsTimeout), cancellationToken);
            }
            catch { }
        }

        public void Set()
        {
            var currentTcs = tcs;
            if (Interlocked.CompareExchange(ref tcs, new TaskCompletionSource<bool>(), currentTcs) == currentTcs)
            {
                currentTcs.SetResult(true);
            }
        }

        //public void Reset()
        //{
        //    while (true)
        //    {
        //        var currentTcs = tcs;
        //        if (!currentTcs.Task.IsCompleted ||
        //            Interlocked.CompareExchange(ref tcs, currentTcs, null) == null)
        //        {
        //            return;
        //        }
        //    }
        //}
    }
}
