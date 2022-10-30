using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;

namespace Lampac.Engine.CORE
{
    public static class HtmlCache
    {
        #region Read
        public static bool Read(string key, out string html)
        {
            try
            {
                string pathfile = getFolder(key);
                if (File.Exists(pathfile))
                {
                    if (Startup.memoryCache.TryGetValue(key, out _))
                    {
                        html = File.ReadAllText(pathfile);
                        return true;
                    }
                }
            }
            catch { }

            html = null;
            return false;
        }
        #endregion

        #region Write
        public static void Write(string key, string html)
        {
            try
            {
                File.WriteAllText(getFolder(key), html);
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
