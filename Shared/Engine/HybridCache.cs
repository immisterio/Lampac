using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.SQL;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Engine
{
    public struct HybridCache
    {
        #region HybridCache
        static IMemoryCache memoryCache;

        static Timer _clearTimer;

        static DateTime _nextClearDb = DateTime.Now.AddMinutes(20);

        static ConcurrentDictionary<string, (DateTime extend, HybridCacheSqlModel cache)> tempDb;

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;

            tempDb = new ConcurrentDictionary<string, (DateTime extend, HybridCacheSqlModel value)>();
            _clearTimer = new Timer(UpdateDB, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        static bool updatingDb = false;
        async static void UpdateDB(object state)
        {
            if (updatingDb || tempDb.Count == 0)
                return;

            try
            {
                updatingDb = true;

                var sqlDb = HybridCacheDb.Write;
                sqlDb.ChangeTracker.Clear();

                if (DateTime.Now > _nextClearDb)
                {
                    var now = DateTime.Now;

                    await sqlDb.files
                         .AsNoTracking()
                         .Where(i => now > i.ex)
                         .ExecuteDeleteAsync();

                    _nextClearDb = DateTime.Now.AddMinutes(20);
                }
                else
                {
                    foreach (var t in tempDb.ToArray())
                    {
                        try
                        {
                            if (t.Value.extend >= DateTime.Now)
                                continue;

                            var doc = sqlDb.files.Find(t.Value.cache.Id);
                            if (doc != null)
                            {
                                sqlDb.files.Remove(doc);
                                await sqlDb.SaveChangesAsync();
                            }

                            sqlDb.files.Add(new HybridCacheSqlModel()
                            {
                                Id = t.Key,
                                ex = t.Value.cache.ex,
                                value = t.Value.cache.value
                            });

                            await sqlDb.SaveChangesAsync();

                            tempDb.TryRemove(t.Key, out _);
                        }
                        catch (Exception ex) { Console.WriteLine("HybridCache: " + ex); }
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine("HybridCache: " + ex); }
            finally
            {
                updatingDb = false;
            }
        }
        #endregion


        #region TryGetValue
        public bool TryGetValue(string key, out object value)
        {
            return memoryCache.TryGetValue(key, out value);
        }

        public bool TryGetValue<TItem>(string key, out TItem value, bool? inmemory = null)
        {
            if (!AppInit.conf.mikrotik)
            {
                if (memoryCache.TryGetValue(key, out value))
                    return true;

                if (ReadCache(key, out value, out bool setmemory))
                {
                    if (setmemory && inmemory != false && AppInit.conf.cache.type == "hybrid" && AppInit.conf.cache.extend > 0)
                        memoryCache.Set(key, value, DateTime.Now.AddSeconds(AppInit.conf.cache.extend));

                    return true;
                }

                return false;
            }

            return memoryCache.TryGetValue(key, out value);
        }
        #endregion

        #region ReadCache
        private bool ReadCache<TItem>(string key, out TItem value, out bool setmemory)
        {
            value = default;
            setmemory = true;

            if (AppInit.conf.cache.type == "mem")
                return false;

            var type = typeof(TItem);
            bool isText = type == typeof(string);

            bool IsDeserialize = type.GetConstructor(Type.EmptyTypes) != null 
                || type.IsValueType 
                || type.IsArray
                || type == typeof(JToken)
                || type == typeof(JObject)
                || type == typeof(JArray);

            if (!isText && !IsDeserialize)
                return false;

            try
            {
                bool deserializeCache(HybridCacheSqlModel doc, out TItem result)
                {
                    result = default;

                    if (doc?.Id == null || DateTime.Now > doc.ex)
                        return false;

                    var eventResult = InvkEvent.HybridCache("read", key, doc.value, doc.ex);

                    if (IsDeserialize)
                        result = JsonConvert.DeserializeObject<TItem>(eventResult.value ?? doc.value);
                    else
                        result = (TItem)Convert.ChangeType(eventResult.value ?? doc.value, type);

                    return true;
                }

                string md5key = CrypTo.md5(key);

                tempDb.TryGetValue(md5key, out var _temp);

                if (_temp.cache != null)
                {
                    setmemory = false;
                    return deserializeCache(_temp.cache, out value);
                }
                else
                {
                    var doc = HybridCacheDb.Read.files.Find(md5key);
                    HybridCacheDb.Read.ChangeTracker.Clear();

                    return deserializeCache(doc, out value);
                }
            }
            catch { }

            return false;
        }
        #endregion


        #region Set
        public TItem Set<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, bool? inmemory = null)
        {
            if (inmemory != true && !AppInit.conf.mikrotik && WriteCache(key, value, absoluteExpiration, default))
                return value;

            if (inmemory != true && !AppInit.conf.mikrotik)
                Console.WriteLine($"set memory: {key} / {DateTime.Now}");

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool? inmemory = null)
        {
            if (inmemory != true && !AppInit.conf.mikrotik && WriteCache(key, value, default, absoluteExpirationRelativeToNow))
                return value;

            if (inmemory != true && !AppInit.conf.mikrotik)
                Console.WriteLine($"set memory: {key} / {DateTime.Now}");

            return memoryCache.Set(key, value, absoluteExpirationRelativeToNow);
        }
        #endregion

        #region WriteCache
        private bool WriteCache<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, TimeSpan absoluteExpirationRelativeToNow)
        {
            if (AppInit.conf.cache.type == "mem")
                return false;

            var type = typeof(TItem);
            bool isText = type == typeof(string);

            bool IsSerialize = type.GetConstructor(Type.EmptyTypes) != null
                || type.IsValueType
                || type.IsArray
                || type == typeof(JToken)
                || type == typeof(JObject)
                || type == typeof(JArray);

            if (!isText && !IsSerialize)
                return false;

            try
            {
                string result;

                if (IsSerialize)
                {
                    result = JsonConvert.SerializeObject(value);
                }
                else
                {
                    result = value.ToString();
                }

                if (absoluteExpiration == default)
                    absoluteExpiration = DateTimeOffset.Now.Add(absoluteExpirationRelativeToNow);

                var eventResult = InvkEvent.HybridCache("write", key, result, absoluteExpiration);
                if (eventResult != default)
                {
                    result = eventResult.value;
                    absoluteExpiration = eventResult.ex;
                }

                var extend = DateTime.Now.AddSeconds(Math.Max(5, AppInit.conf.cache.extend));

                tempDb.TryAdd(CrypTo.md5(key), (extend, new HybridCacheSqlModel()
                {
                    Id = CrypTo.md5(key),
                    ex = absoluteExpiration.DateTime,
                    value = result
                }));

                return true;
            }
            catch { }

            return false;
        }
        #endregion
    }
}
