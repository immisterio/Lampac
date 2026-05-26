using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services.Buckets;

public static class BucketHeaders
{
    #region static
    sealed class BucketHeadersModel
    {
        public IReadOnlyList<HeadersModel> Headers { get; }

        private long _ticks;

        public long Ticks
            => Volatile.Read(ref _ticks);

        public BucketHeadersModel(IReadOnlyList<HeadersModel> headers)
        {
            Headers = headers;
            Touch();
        }

        public void Touch()
        {
            Volatile.Write(ref _ticks, DateTime.UtcNow.Ticks);
        }
    }

    static readonly ConcurrentDictionary<ulong, BucketHeadersModel> bk = new();

    static readonly Timer _timer = new(_ =>
    {
        var expired = DateTime.UtcNow.AddHours(-3).Ticks;

        foreach (var item in bk)
        {
            if (expired > item.Value.Ticks)
                bk.TryRemove(item.Key, out BucketHeadersModel _);
        }
    }, null, TimeSpan.Zero, TimeSpan.FromMinutes(10));

    public static int Stat_ContTempDb => bk.Count;
    #endregion

    public static bool TryGetValue(ulong H1, out IReadOnlyList<HeadersModel> headers)
    {
        if (bk.TryGetValue(H1, out var model))
        {
            model.Touch();
            headers = model.Headers;
            return true;
        }

        headers = null;
        return false;
    }

    public static ulong Hash(string prefix, IReadOnlyList<HeadersModel> headers)
    {
        var hash = Fnv1a.Empty;
        Fnv1a.Append(ref hash, prefix);

        foreach (var h in headers)
        {
            Fnv1a.Append(ref hash, h.name);
            Fnv1a.Append(ref hash, h.val);
        }

        return hash.H1;
    }

    public static ulong AddOrUpdate(string prefix, IReadOnlyList<HeadersModel> headers)
    {
        var hash = Fnv1a.Empty;
        Fnv1a.Append(ref hash, prefix);

        foreach (var h in headers)
        {
            Fnv1a.Append(ref hash, h.name);
            Fnv1a.Append(ref hash, h.val);
        }

        AddOrUpdate(hash.H1, headers);
        return hash.H1;
    }

    public static void AddOrUpdate(ulong H1, IReadOnlyList<HeadersModel> headers)
    {
        bk[H1] = new BucketHeadersModel(headers);
    }
}