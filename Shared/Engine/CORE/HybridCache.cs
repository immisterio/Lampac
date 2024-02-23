using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Engine.CORE
{
    public class HybridCache
    {
        #region HybridCache
        static IMemoryCache memoryCache;

        static ConcurrentDictionary<string, DateTimeOffset> condition = new ConcurrentDictionary<string, DateTimeOffset>();

        static string folderCache => "cache/fdb";

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
            string conditionPath = $"{folderCache}/condition.json";
            Directory.CreateDirectory(folderCache);

            if (File.Exists(conditionPath))
            {
                try
                {
                    foreach (var item in JsonConvert.DeserializeObject<ConcurrentDictionary<string, DateTimeOffset>>(BrotliTo.Decompress(File.ReadAllBytes(conditionPath))))
                    {
                        if (item.Value > DateTimeOffset.Now)
                        {
                            memoryCache.Set(item.Key, (byte)0, item.Value);
                            condition.AddOrUpdate(item.Key, item.Value, (k, v) => item.Value);
                        }
                    }
                }
                catch { }
            }

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(10));

                    try
                    {
                        foreach (string infile in Directory.EnumerateFiles(folderCache, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                string md5key = Path.GetFileName(infile);
                                if (md5key == "condition.json")
                                    continue;

                                if (!memoryCache.TryGetValue($"{folderCache}:{md5key}", out _))
                                    File.Delete(infile);
                            }
                            catch { }
                        }

                        foreach (var item in condition.Where(i => DateTimeOffset.Now > i.Value))
                            condition.TryRemove(item);

                        File.WriteAllBytes(conditionPath, BrotliTo.Compress(JsonConvert.SerializeObject(condition)));
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

        public bool TryGetValue<TItem>(string key, out TItem value, bool inmemory = false)
        {
            if (!inmemory && !AppInit.conf.mikrotik)
            {
                int extend = 2;

                if (AppInit.conf.typecache == "hybrid" && memoryCache.TryGetValue(key, out value))
                {
                    if (condition.TryGetValue($"{folderCache}:{CrypTo.md5(key)}", out DateTimeOffset ex) && ex > DateTime.Now.AddMinutes(extend))
                        memoryCache.Set(key, value, TimeSpan.FromMinutes(extend));

                    return true;
                }

                if (ReadCache(key, out value))
                {
                    if (AppInit.conf.typecache == "hybrid")
                        memoryCache.Set(key, value, TimeSpan.FromMinutes(extend));

                    return true;
                }
            }

            return memoryCache.TryGetValue(key, out value);
        }
        #endregion

        #region ReadCache
        public bool ReadCache<TItem>(string key, out TItem value)
        {
            value = default;
            if (AppInit.conf.typecache == "mem")
                return false;

            if (!memoryCache.TryGetValue($"{folderCache}:{CrypTo.md5(key)}", out _))
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
        public TItem Set<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, bool inmemory = false)
        {
            if (!inmemory && !AppInit.conf.mikrotik && WriteCache(key, value, absoluteExpiration, default))
                return value;

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool inmemory = false)
        {
            if (!inmemory && !AppInit.conf.mikrotik && WriteCache(key, value, default, absoluteExpirationRelativeToNow))
                return value;

            return memoryCache.Set(key, value, absoluteExpirationRelativeToNow);
        }
        #endregion

        #region WriteCache
        public bool WriteCache<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, TimeSpan absoluteExpirationRelativeToNow)
        {
            if (AppInit.conf.typecache == "mem")
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
                    memoryCache.Set($"{folderCache}:{md5key}", (byte)0, absoluteExpiration);
                else
                {
                    memoryCache.Set($"{folderCache}:{md5key}", (byte)0, absoluteExpirationRelativeToNow);
                    absoluteExpiration = new DateTimeOffset(absoluteExpirationRelativeToNow.Ticks, TimeSpan.Zero);
                }

                condition.AddOrUpdate($"{folderCache}:{md5key}", absoluteExpiration, (k, v) => absoluteExpiration);

                return true;
            }
            catch { }

            return false;
        }
        #endregion
    }
}
