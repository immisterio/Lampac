using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
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

        static DateTime _nextClearDb = DateTime.Now.AddMinutes(5);

        static ConcurrentDictionary<string, HybridCacheSqlModel> tempDb;

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;

            tempDb = new ConcurrentDictionary<string, HybridCacheSqlModel>();
            _clearTimer = new Timer(UpdateDB, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        static bool updatingDb = false;
        static void UpdateDB(object state)
        {
            if (updatingDb)
                return;

            try
            {
                updatingDb = true;

                using (var sqlDb = new HybridCacheContext())
                {
                    if (DateTime.Now > _nextClearDb)
                    {
                        var now = DateTime.Now;

                        sqlDb.files
                             .AsNoTracking()
                             .Where(i => now > i.ex)
                             .ExecuteDelete();

                        _nextClearDb = DateTime.Now.AddHours(1);
                    }
                    else
                    {
                        foreach (var t in tempDb.ToArray())
                        {
                            try
                            {
                                var doc = sqlDb.files.Find(t.Key);
                                if (doc != null)
                                {
                                    doc.ex = t.Value.ex;
                                    doc.value = t.Value.value;
                                }
                                else
                                {
                                    sqlDb.files.Add(new HybridCacheSqlModel()
                                    {
                                        Id = t.Key,
                                        ex = t.Value.ex,
                                        value = t.Value.value
                                    });
                                }

                                sqlDb.SaveChanges();
                                tempDb.TryRemove(t.Key, out _);
                            }
                            catch (Exception ex) { Console.WriteLine("HybridCache / UpdateDb: " + ex); }
                        }
                    }
                }
            }
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
                if (AppInit.conf.cache.type == "hybrid" && memoryCache.TryGetValue(key, out value))
                    return true;

                if (ReadCache(key, out value))
                {
                    if (inmemory != false && AppInit.conf.cache.type == "hybrid" && AppInit.conf.cache.extend > 0)
                        memoryCache.Set(key, value, DateTime.Now.AddSeconds(AppInit.conf.cache.extend));

                    return true;
                }
            }

            return memoryCache.TryGetValue(key, out value);
        }
        #endregion

        #region ReadCache
        private bool ReadCache<TItem>(string key, out TItem value)
        {
            value = default;
            if (AppInit.conf.cache.type == "mem")
                return false;

            var type = typeof(TItem);
            bool isText = type == typeof(string);
            bool isConstructor = type.GetConstructor(Type.EmptyTypes) != null;

            if (!isText && !isConstructor && !type.IsValueType && !type.IsArray)
                return false;

            try
            {
                using (var sqlDb = new HybridCacheContext())
                {
                    string md5key = CrypTo.md5(key);
                    tempDb.TryGetValue(key, out HybridCacheSqlModel _temp);
                    if (_temp != null)
                        _temp.Id = md5key;

                    var doc = _temp ?? sqlDb.files.Find(md5key);

                    if (doc?.Id == null || DateTime.Now > doc.ex)
                        return false;

                    var eventResult = InvkEvent.HybridCache("read", key, doc.value, doc.ex);

                    if (isConstructor || type.IsValueType || type.IsArray)
                        value = JsonConvert.DeserializeObject<TItem>(eventResult.value ?? doc.value);
                    else
                        value = (TItem)Convert.ChangeType(eventResult.value ?? doc.value, type);

                    return true;
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
            {
                if (AppInit.conf.cache.type == "hybrid" && inmemory != false && AppInit.conf.cache.extend > 0)
                    memoryCache.Set(key, value, DateTime.Now.AddSeconds(AppInit.conf.cache.extend));

                return value;
            }

            if (inmemory != true && !AppInit.conf.mikrotik)
                Console.WriteLine($"set memory: {key} / {DateTime.Now}");

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool? inmemory = null)
        {
            if (inmemory != true && !AppInit.conf.mikrotik && WriteCache(key, value, default, absoluteExpirationRelativeToNow))
            {
                if (AppInit.conf.cache.type == "hybrid" && inmemory != false && AppInit.conf.cache.extend > 0)
                    memoryCache.Set(key, value, DateTime.Now.AddSeconds(AppInit.conf.cache.extend));

                return value;
            }

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
            bool isConstructor = type.GetConstructor(Type.EmptyTypes) != null;

            if (!isText && !isConstructor && !type.IsValueType && !type.IsArray)
                return false;

            try
            {
                string result;

                if (isConstructor || type.IsValueType || type.IsArray)
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

                tempDb.TryAdd(CrypTo.md5(key), new HybridCacheSqlModel()
                {
                    ex = absoluteExpiration.DateTime,
                    value = result
                });

                return true;
            }
            catch { }

            return false;
        }
        #endregion
    }
}
