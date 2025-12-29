using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.SQL;
using System.Collections.Concurrent;
using System.Threading;

namespace Shared.Engine
{
    public class HybridCache
    {
        #region HybridCache
        static IMemoryCache memoryCache;

        static Timer _clearTimer;

        static DateTime _nextClearDb = DateTime.Now.AddMinutes(5);

        readonly static ConcurrentDictionary<string, (DateTime extend, bool IsSerialize, DateTime ex, object value)> tempDb = new();

        public static int Stat_ContTempDb => tempDb.IsEmpty ? 0 : tempDb.Count;

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
            _clearTimer = new Timer(UpdateDB, null, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));
        }

        static int _updatingDb = 0;
        async static void UpdateDB(object state)
        {
            if (tempDb.IsEmpty)
                return;

            if (Interlocked.Exchange(ref _updatingDb, 1) == 1)
                return;

            try
            {
                var now = DateTime.Now;

                if (now > _nextClearDb)
                {
                    _nextClearDb = DateTime.Now.AddMinutes(5);

                    using (var sqlDb = new HybridCacheContext())
                    {
                        await sqlDb.files
                            .Where(i => now > i.ex)
                            .ExecuteDeleteAsync();
                    }
                }
                else
                {
                    var array = tempDb
                        .Where(t => now > t.Value.extend)
                        .Take(500)
                        .ToArray();

                    if (array.Length > 0)
                    {
                        using (var sqlDb = new HybridCacheContext())
                        {
                            var delete_ids = array.Select(k => k.Key).ToHashSet();
                            if (delete_ids.Count > 0)
                            {
                                await sqlDb.files
                                    .Where(x => delete_ids.Contains(x.Id))
                                    .ExecuteDeleteAsync();
                            }

                            var hash_ids = new HashSet<string>();

                            foreach (var t in array)
                            {
                                if (t.Value.ex > now && hash_ids.Add(t.Key))
                                {
                                    sqlDb.files.Add(new HybridCacheSqlModel()
                                    {
                                        Id = t.Key,
                                        ex = t.Value.ex,
                                        value = t.Value.IsSerialize
                                            ? JsonConvert.SerializeObject(t.Value.value)
                                            : t.Value.value.ToString()
                                    });
                                }
                            }

                            await sqlDb.SaveChangesAsync();

                            foreach (var t in array)
                                tempDb.TryRemove(t.Key, out _);
                        }
                    }
                }
            }
            catch (Exception ex) 
            { 
                Console.WriteLine("HybridCache: " + ex); 
            }
            finally
            {
                Volatile.Write(ref _updatingDb, 0);
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
            if (!AppInit.conf.mikrotik == false && AppInit.conf.cache.type != "mem")
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
                string md5key = CrypTo.md5(key);

                if (tempDb.TryGetValue(md5key, out var _temp))
                {
                    setmemory = false;
                    value = (TItem)_temp.value;
                    return true;
                }
                else
                {
                    using (var sqlDb = HybridCacheContext.Factory != null 
                        ? HybridCacheContext.Factory.CreateDbContext()
                        : new HybridCacheContext())
                    {
                        var doc = sqlDb.files.Find(md5key);

                        if (doc?.Id == null || DateTime.Now > doc.ex)
                            return false;

                        if (IsDeserialize)
                            value = JsonConvert.DeserializeObject<TItem>(doc.value);
                        else
                            value = (TItem)Convert.ChangeType(doc.value, type);

                        return true;
                    }
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

            string md5key = CrypTo.md5(key);

            // кеш уже получен от другого rch клиента
            if (tempDb.ContainsKey(md5key))
                return true;

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
                if (absoluteExpiration == default)
                    absoluteExpiration = DateTimeOffset.Now.Add(absoluteExpirationRelativeToNow);

                /// защита от асинхронных rch запросов которые приходят в рамках 12 секунд
                /// дополнительный кеш для сериалов, что бы выборка сезонов/озвучки не дергала sql 
                var extend = DateTime.Now.AddSeconds(Math.Max(15, AppInit.conf.cache.extend));

                tempDb.TryAdd(md5key, (extend, IsSerialize, absoluteExpiration.DateTime, value));

                return true;
            }
            catch { }

            return false;
        }
        #endregion
    }
}
