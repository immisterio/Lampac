using System.Text.RegularExpressions;

namespace Shared.Engine
{
    public static class FileCache
    {
        private static readonly object _lock = new object();

        static Dictionary<string, (DateTime lockTime, string value)> db = new();

        public static string ReadAllText(string path)
        {
            return ReadAllText(path, true);
        }

        public static string ReadAllText(string path, bool saveCache)
        {
            var secondCache = DateTime.Now.AddSeconds(AppInit.conf.multiaccess ? 5 : 1);

            try
            {
                lock (_lock)
                {
                    if (db.TryGetValue(path, out var cache))
                    {
                        if (cache.lockTime > DateTime.Now)
                            return cache.value;
                    }

                    string extension = Path.GetExtension(path);
                    string mypath = Regex.Replace(path, $"{extension}$", $".my{extension}");

                    if (!File.Exists(mypath))
                    {
                        if (!File.Exists(path))
                        {
                            db.TryAdd(path, (secondCache, string.Empty));
                            return string.Empty;
                        }

                        mypath = path;
                    }

                    cache = (secondCache, File.ReadAllText(mypath));

                    if (saveCache)
                        db.TryAdd(path, cache);

                    return cache.value;
                }
            }
            catch 
            {
                return string.Empty;
            }
        }
    }
}
