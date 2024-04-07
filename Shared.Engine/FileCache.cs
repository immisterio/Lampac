using System.Collections.Concurrent;

namespace Shared.Engine
{
    public static class FileCache
    {
        static ConcurrentDictionary<string, (DateTime lastWriteTime, string value)> db = new ConcurrentDictionary<string, (DateTime, string)>();

        public static string ReadAllText(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return string.Empty;

                var lastWriteTime = File.GetLastWriteTime(path);

                if (!db.TryGetValue(path, out var cache) || lastWriteTime > cache.lastWriteTime)
                {
                    cache = (lastWriteTime, File.ReadAllText(path));
                    db.AddOrUpdate(path, cache, (k,v) => cache);
                }

                return cache.value;

            }
            catch { }

            return string.Empty;
        }
    }
}
