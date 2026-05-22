using System.Collections.Concurrent;

namespace Shared.Services;

public static class FileCache
{
    private readonly record struct CacheEntry(DateTime lockTime, string value);
    private readonly static ConcurrentDictionary<string, CacheEntry> db = new();

    public static string ReadAllText(string path)
        => ReadAllText(path, null, true);

    public static string ReadAllText(string path, string mypath)
        => ReadAllText(path, mypath, true);

    public static string ReadAllText(string path, string overridePath, bool saveCache)
    {
        try
        {
            string key = overridePath ?? path;
            var now = DateTime.UtcNow;

            if (db.TryGetValue(key, out CacheEntry cache))
            {
                if (cache.lockTime > now)
                    return cache.value;
            }

            bool isOverride = false;
            if (overridePath != null)
            {
                string testPath = $"plugins/override/{overridePath}";
                if (File.Exists(testPath))
                {
                    isOverride = true;
                    path = testPath;
                }
            }

            if (isOverride == false)
            {
                if (File.Exists(path) == false)
                {
                    if (saveCache)
                        db[key] = new(now.AddMinutes(1), string.Empty);

                    return string.Empty;
                }
            }

            string value = File.ReadAllText(path);

            if (saveCache)
                db[key] = new(now.AddMinutes(10), value);

            return value;
        }
        catch
        {
            return string.Empty;
        }
    }
}
