using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Services.Buckets;

public static class BucketHeaders
{
    static readonly ConcurrentDictionary<ulong, IReadOnlyList<HeadersModel>> bk = new();

    static readonly Timer _timer = new(_ =>
    {
        bk.Clear();
    }, null, TimeSpan.FromMinutes(20), TimeSpan.FromMinutes(20));


    public static bool TryGetValue(ulong H1, out IReadOnlyList<HeadersModel> headers)
    {
        return bk.TryGetValue(H1, out headers);
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
        bk[H1] = headers;
    }
}
