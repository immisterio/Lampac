using Microsoft.Extensions.Caching.Memory;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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
            using (var md5 = MD5.Create())
            {
                byte[] result = md5.ComputeHash(Encoding.UTF8.GetBytes(key));
                string md5key = BitConverter.ToString(result).Replace("-", "").ToLower();

                Directory.CreateDirectory($"cache/html/{md5key[0]}");
                return $"cache/html/{md5key[0]}/{md5key}";
            }
        }
        #endregion
    }
}
