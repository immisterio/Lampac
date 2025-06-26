using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public static class FileCache
    {
        private static readonly object _lock = new object();

        static Dictionary<string, (DateTime lastWriteTime, DateTime lockTime, string mypath, string value)> db = new Dictionary<string, (DateTime, DateTime, string, string)>();

        public static string ReadAllText(string path)
        {
            return ReadAllText(path, true, out _);
        }

        public static string ReadAllText(string path, bool saveCache)
        {
            return ReadAllText(path, saveCache, out _);
        }

        public static string ReadAllText(string path, bool saveCache, out DateTime lastWriteTime)
        {
            lastWriteTime = default;

            try
            {
                lock (_lock)
                {
                    if (db.TryGetValue(path, out var cache))
                    {
                        if (cache.lockTime > DateTime.Now)
                            return cache.value;

                        lastWriteTime = File.GetLastWriteTime(cache.mypath);
                        if (lastWriteTime > cache.lastWriteTime)
                        {
                            cache = (lastWriteTime, DateTime.Now.AddSeconds(5), cache.mypath, File.ReadAllText(cache.mypath));
                            db[path] = cache;
                        }
                        else
                        {
                            cache = (lastWriteTime, DateTime.Now.AddSeconds(5), cache.mypath, cache.value);
                            db[path] = cache;
                        }

                        return cache.value;
                    }
                    else
                    {
                        string extension = Path.GetExtension(path);
                        string mypath = Regex.Replace(path, $"{extension}$", $".my{extension}");

                        if (!File.Exists(mypath))
                        {
                            if (!File.Exists(path))
                                return string.Empty;

                            mypath = path;
                        }

                        lastWriteTime = File.GetLastWriteTime(mypath);

                        cache = (lastWriteTime, DateTime.Now.AddSeconds(5), mypath, File.ReadAllText(mypath));
                        
                        if (saveCache)
                            db.TryAdd(path, cache);

                        return cache.value;
                    }
                }
            }
            catch 
            {
                return string.Empty;
            }
        }
    }
}
