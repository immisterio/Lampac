using Lampac;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        static int extend = 1;

        static ConcurrentDictionary<string, DateTimeOffset> condition = new ConcurrentDictionary<string, DateTimeOffset>();

        static readonly object lockObject = new object();

        static string folderCache => "cache/fdb";

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
            string conditionPath = $"{folderCache}/condition.json";

            lock (lockObject)
            {
                if (File.Exists(conditionPath))
                {
                    try
                    {
                        foreach (var item in JsonConvert.DeserializeObject<Dictionary<string, DateTimeOffset>>(BrotliTo.Decompress(conditionPath)))
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
                else
                {
                    Directory.CreateDirectory(folderCache);
                }
            }

            ThreadPool.QueueUserWorkItem(async _ => 
            {
                while (true)
                {
                    await Task.Delay(TimeSpan.FromMinutes(5));

                    try
                    {
                        foreach (string infile in Directory.GetFiles(folderCache, "*", SearchOption.AllDirectories))
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

                        BrotliTo.Compress(conditionPath, JsonConvert.SerializeObject(condition));
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

            if (!memoryCache.TryGetValue($"{folderCache}:{CrypTo.md5(key)}", out _)) // тут byte, не TItem!
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
                string content = BrotliTo.Decompress(path);

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
            {
                if (AppInit.conf.typecache == "hybrid")
                    memoryCache.Set(key, value, DateTime.Now.AddMinutes(extend));

                return value;
            }

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool inmemory = false)
        {
            if (!inmemory && !AppInit.conf.mikrotik && WriteCache(key, value, default, absoluteExpirationRelativeToNow))
            {
                if (AppInit.conf.typecache == "hybrid")
                    memoryCache.Set(key, value, DateTime.Now.AddMinutes(extend));

                return value;
            }

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
                string md5key = CrypTo.md5(key);

                if (isConstructor || isValueType)
                {
                    BrotliTo.Compress($"{folderCache}/{md5key}", JsonConvert.SerializeObject(value));
                }
                else
                {
                    BrotliTo.Compress($"{folderCache}/{md5key}", value.ToString());
                }

                if (absoluteExpiration != default)
                    memoryCache.Set($"{folderCache}:{md5key}", (byte)0, absoluteExpiration);
                else
                {
                    memoryCache.Set($"{folderCache}:{md5key}", (byte)0, absoluteExpirationRelativeToNow);
                    absoluteExpiration = DateTimeOffset.Now.Add(absoluteExpirationRelativeToNow);
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
