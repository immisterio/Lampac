using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Engine.CORE
{
    public class HybridCache
    {
        #region HybridCache
        static IMemoryCache memoryCache;

        static string folderCache => "cache/fdb";

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
            Directory.CreateDirectory(folderCache);

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(20));

                    try
                    {
                        foreach (string infile in Directory.EnumerateFiles(folderCache, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                string md5key = Path.GetFileName(infile);
                                if (!memoryCache.TryGetValue($"{folderCache}:{md5key}", out _))
                                    File.Delete(infile);
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            });
        }
        #endregion


        #region TryGetValue
        public bool TryGetValue(string key, out object value)
        {
            return memoryCache.TryGetValue(key, out value);
        }

        public bool TryGetValue<TItem>(string key, out TItem value)
        {
            if (ReadCache(key, out value))
                return true;

            return memoryCache.TryGetValue(key, out value);
        }
        #endregion

        #region ReadCache
        public bool ReadCache<TItem>(string key, out TItem value)
        {
            value = default;
            if (AppInit.conf.typecache != "file")
                return false;

            if (!memoryCache.TryGetValue($"{folderCache}:{key}", out _))
                return false;

            var type = typeof(TItem);
            bool isText = type == typeof(string);
            bool isConstructor = type.GetConstructor(Type.EmptyTypes) != null;
            bool isValueType = type.IsValueType;

            if (!isText && !isConstructor && !isValueType)
                return false;

            string path = $"{folderCache}/{CrypTo.md5(key)}";
            if (!File.Exists(path))
                return false;

            try
            {
                string content = BrotliTo.Decompress(File.ReadAllBytes(path));

                if (isConstructor || isValueType)
                    value = JsonConvert.DeserializeObject<TItem>(content);
                else
                    value = (TItem)Convert.ChangeType(content, type);

                return true;
            }
            catch { }

            return false;
        }
        #endregion


        #region Set
        public TItem Set<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration)
        {
            if (WriteCache(key, value, absoluteExpiration, default))
                return value;

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow)
        {
            if (WriteCache(key, value, default, absoluteExpirationRelativeToNow))
                return value;

            return memoryCache.Set(key, value, absoluteExpirationRelativeToNow);
        }
        #endregion

        #region WriteCache
        public bool WriteCache<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, TimeSpan absoluteExpirationRelativeToNow)
        {
            if (AppInit.conf.typecache != "file")
                return false;

            var type = typeof(TItem);
            bool isText = type == typeof(string);
            bool isConstructor = type.GetConstructor(Type.EmptyTypes) != null;
            bool isValueType = type.IsValueType;

            if (!isText && !isConstructor && !isValueType)
                return false;

            try
            {
                byte[] array = null;

                if (isConstructor || isValueType)
                {
                    array = BrotliTo.Compress(JsonConvert.SerializeObject(value));
                }
                else
                {
                    array = BrotliTo.Compress(value.ToString());
                }

                string md5key = CrypTo.md5(key);
                File.WriteAllBytes($"{folderCache}/{md5key}", array);

                if (absoluteExpiration != default)
                {
                    memoryCache.Set($"{folderCache}:{key}", (byte)0, absoluteExpiration);
                    memoryCache.Set($"{folderCache}:{md5key}", (byte)0, absoluteExpiration);
                }
                else
                {
                    memoryCache.Set($"{folderCache}:{key}", (byte)0, absoluteExpirationRelativeToNow);
                    memoryCache.Set($"{folderCache}:{md5key}", (byte)0, absoluteExpirationRelativeToNow);
                }

                return true;
            }
            catch { }

            return false;
        }
        #endregion
    }
}
