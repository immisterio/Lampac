using Newtonsoft.Json;
using Shared.Services;
using Shared.Services.Utilities;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace GStreamer.Services;

public static class GService
{
    static ConcurrentDictionary<ulong, GStask> tasks = new();

    static int cleanupRunning;
    static readonly Timer cleanupTimer = new(
        static _ => CleanupInactive(),
        null,
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(1)
    );

    public static async Task<GStask> GetOrAdd(string sourceUrl, string uid)
    {
        if (string.IsNullOrEmpty(sourceUrl) || string.IsNullOrEmpty(uid))
            return null;

        var hash = Fnv1a.Hash(sourceUrl);
        Fnv1a.Append(ref hash, uid);

        if (tasks.TryGetValue(hash.H1, out var task))
        {
            task.UpdateLastActive();
            return task;
        }

        if (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host))
        {
            throw new ArgumentException("Invalid URL", nameof(sourceUrl));
        }

        string location = await Http.GetLocation(sourceUrl);
        if (location != null)
            sourceUrl = location;

        var probe = await GSProbe.Get(sourceUrl);
        //Console.WriteLine(JsonConvert.SerializeObject(probe, Formatting.Indented));
        if (probe == null)
            return null;

        if (!probe.IsH264 && !probe.IsH265 && !probe.IsAV1 && !probe.IsVP9)
            return null;

        task = new GStask(probe, sourceUrl, hash.H1);
        tasks[hash.H1] = task;

        return task;
    }

    public static GStask Get(ulong id)
    {
        if (tasks.TryGetValue(id, out var task))
        {
            task.UpdateLastActive();
            return task;
        }

        return null;
    }

    public static bool TryRemove(ulong id)
    {
        if (tasks.TryRemove(id, out var task))
        {
            task.Dispose();
            return true;
        }

        return false;
    }

    static void CleanupInactive()
    {
        if (Interlocked.Exchange(ref cleanupRunning, 1) == 1)
            return;

        try
        {
            var minActiveTime = DateTime.UtcNow - TimeSpan.FromMinutes(5);

            foreach (var item in tasks)
            {
                var id = item.Key;
                var task = item.Value;

                if (task.lastActive > minActiveTime)
                    continue;

                if (tasks.TryRemove(id, out var removed))
                    removed.Dispose();
            }
        }
        catch { }
        finally
        {
            Volatile.Write(ref cleanupRunning, 0);
        }
    }

    public static void Dispose()
    {
        foreach (var item in tasks)
            item.Value.Dispose();
    }
}
