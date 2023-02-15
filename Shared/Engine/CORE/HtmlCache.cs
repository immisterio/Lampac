using Microsoft.Extensions.Caching.Memory;
using Shared;
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
                if (AppInit.conf.jac.cachetype == "mem")
                {
                    if (Startup.memoryCache.TryGetValue(key, out byte[] html))
                        return (true, false, BrotliTo.Decompress(html));

                    return (false, false, null);
                }

                string pathfile = getFolder(key);
                bool cache = Startup.memoryCache.TryGetValue(key, out _);

                if (File.Exists(pathfile))
                    return (cache, false, BrotliTo.Decompress(await File.ReadAllBytesAsync(pathfile)));
                else
                    return (false, cache, null);
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
                if (AppInit.conf.jac.htmlCacheToMinutes > 0)
                {
                    if (AppInit.conf.jac.cachetype == "mem")
                    {
                        Startup.memoryCache.Set(key, BrotliTo.Compress(html), DateTime.Now.AddMinutes(AppInit.conf.jac.htmlCacheToMinutes));
                    }
                    else
                    {
                        await File.WriteAllBytesAsync(getFolder(key), BrotliTo.Compress(html));
                        Startup.memoryCache.Set(key, string.Empty, DateTime.Now.AddMinutes(AppInit.conf.jac.htmlCacheToMinutes));
                    }
                }
            }
            catch { }
        }
        #endregion

        #region EmptyCache
        public static void EmptyCache(string key)
        {
            if (AppInit.conf.jac.emptycache)
                Startup.memoryCache.Set(key, string.Empty, DateTime.Now.AddMinutes(Math.Max(1, AppInit.conf.jac.htmlCacheToMinutes)));
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
