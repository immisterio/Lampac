using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class HtmlCache
    {
        #region Read
        async public static ValueTask<(bool cache, string html)> Read(string key)
        {
            try
            {
                string pathfile = getFolder(key);
                if (File.Exists(pathfile))
                {
                    if (Startup.memoryCache.TryGetValue(key, out _))
                        return (true, await File.ReadAllTextAsync(pathfile));
                }
            }
            catch { }

            return (false, null);
        }
        #endregion

        #region Write
        async public static ValueTask Write(string key, string html)
        {
            try
            {
                await File.WriteAllTextAsync(getFolder(key), html);
                Startup.memoryCache.Set(key, 0, DateTime.Now.AddMinutes(AppInit.conf.htmlCacheToMinutes));
            }
            catch { }
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
