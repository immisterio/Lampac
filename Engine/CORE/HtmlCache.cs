using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class HtmlCache
    {
        #region Read
        async public static ValueTask<(bool cache, bool emptycache, string html)> Read(string key)
        {
            try
            {
                if (AppInit.conf.cachetype == "mem")
                {
                    if (Startup.memoryCache.TryGetValue(key, out string html))
                        return (true, false, html);

                    return (false, false, null);
                }

                if (Startup.memoryCache.TryGetValue(key, out _))
                {
                    string pathfile = getFolder(key);

                    if (File.Exists(pathfile))
                        return (true, false, await File.ReadAllTextAsync(pathfile));
                    else
                        return (false, true, null);
                }
            }
            catch { }

            return (false, false, null);
        }
        #endregion

        #region Write
        async public static ValueTask Write(string key, string html)
        {
            try
            {
                if (AppInit.conf.cachetype == "mem")
                {
                    Startup.memoryCache.Set(key, html, DateTime.Now.AddMinutes(AppInit.conf.htmlCacheToMinutes));
                }
                else
                {
                    await File.WriteAllTextAsync(getFolder(key), html);
                    Startup.memoryCache.Set(key, string.Empty, DateTime.Now.AddMinutes(AppInit.conf.htmlCacheToMinutes));
                }
            }
            catch { }
        }
        #endregion

        #region EmptyCache
        public static void EmptyCache(string key)
        {
            if (AppInit.conf.emptycache)
                Startup.memoryCache.Set(key, string.Empty, DateTime.Now.AddMinutes(AppInit.conf.htmlCacheToMinutes));
        }
        #endregion

        #region getFolder
        static string getFolder(string key)
        {
            string md5key = CrypTo.md5(key);
            Directory.CreateDirectory($"cache/html/{md5key[0]}");
            return $"cache/html/{md5key[0]}/{md5key}";
        }
        #endregion
    }
}
