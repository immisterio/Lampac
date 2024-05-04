using Jackett;
using JacRed.Models;
using Lampac.Models.AppConf;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Shared;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Lampac.Engine.CORE
{
    public static class JackettCache
    {
        static JacConf jac => ModInit.conf.Jackett;

        #region Invoke
        async public static Task<bool> Invoke(string cachekey, ConcurrentBag<TorrentDetails> torrents, Func<ValueTask<List<TorrentDetails>>> parse)
        {
            var cread = Read(cachekey);

            if (cread.emptycache)
                return false;

            var result = new List<TorrentDetails>();

            if (!cread.cache)
            {
                result = await parse();
                Write(cachekey, result);
            }
            else { result = cread.torrents; }

            if (result != null && result.Count > 0)
            {
                foreach (TorrentDetails torrent in result)
                    torrents.Add(torrent);

                return true;
            }

            return false;
        }
        #endregion

        #region Read
        public static (bool cache, bool emptycache, List<TorrentDetails> torrents) Read(string key)
        {
            if (!jac.cache)
                return default;

            try
            {
                if (jac.cachetype == "mem")
                {
                    if (Startup.memoryCache.TryGetValue(key, out List<TorrentDetails> torrents))
                        return (true, false, torrents);

                    return (false, false, null);
                }

                string pathfile = getFolder(key, createDirectory: false);
                bool cache = Startup.memoryCache.TryGetValue(key, out _);

                if (File.Exists(pathfile))
                    return (cache, false, JsonConvert.DeserializeObject<List<TorrentDetails>>(BrotliTo.Decompress(pathfile)));
                else
                    return (false, cache, null);
            }
            catch { }

            return default;
        }
        #endregion

        #region Write
        public static void Write(string key, List<TorrentDetails> torrents)
        {
            if (!jac.cache)
                return;

            try
            {
                if (torrents == null || torrents.Count == 0)
                {
                    EmptyCache(key);
                    return;
                }

                if (jac.cacheToMinutes > 0)
                {
                    if (jac.cachetype == "mem")
                    {
                        Startup.memoryCache.Set(key, torrents, DateTime.Now.AddMinutes(jac.cacheToMinutes));
                    }
                    else
                    {
                        BrotliTo.Compress(getFolder(key), JsonConvert.SerializeObject(torrents));
                        Startup.memoryCache.Set(key, string.Empty, DateTime.Now.AddMinutes(jac.cacheToMinutes));
                    }
                }
            }
            catch { }
        }
        #endregion

        #region EmptyCache
        static void EmptyCache(string key)
        {
            if (jac.emptycache && jac.cache)
                Startup.memoryCache.Set(key, string.Empty, DateTime.Now.AddMinutes(10));
        }
        #endregion

        #region getFolder
        static string getFolder(string key, bool createDirectory = true)
        {
            string md5key = CrypTo.md5(key);

            if (createDirectory)
                Directory.CreateDirectory($"cache/jackett/{md5key[0]}");

            return $"cache/jackett/{md5key[0]}/{md5key}";
        }
        #endregion
    }
}
