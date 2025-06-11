using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public static class FileCache
    {
        static ConcurrentDictionary<string, (DateTime lastWriteTime, string value)> db = new ConcurrentDictionary<string, (DateTime, string)>();

        public static string ReadAllText(string path)
        {
            return ReadAllText(path, out _);
        }

        public static string ReadAllText(string path, out DateTime lastWriteTime)
        {
            lastWriteTime = default;

            try
            {
                string extension = Path.GetExtension(path);
                string mypath = Regex.Replace(path, $"{extension}$", $".my{extension}");
                if (File.Exists(mypath))
                    path = mypath;

                if (!File.Exists(path))
                    return string.Empty;

                lastWriteTime = File.GetLastWriteTime(path);

                if (!db.TryGetValue(path, out var cache) || lastWriteTime > cache.lastWriteTime)
                {
                    cache = (lastWriteTime, File.ReadAllText(path));
                    db.AddOrUpdate(path, cache, (k, v) => cache);
                }

                return cache.value;

            }
            catch { }

            return string.Empty;
        }
    }
}
