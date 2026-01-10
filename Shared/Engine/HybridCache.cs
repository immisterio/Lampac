using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine.Utilities;
using Shared.Models;
using Shared.Models.SQL;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;

namespace Shared.Engine
{
    public class HybridCache
    {
        #region static
        static readonly ThreadLocal<JsonSerializer> _serializer = new ThreadLocal<JsonSerializer>(JsonSerializer.CreateDefault);

        static IMemoryCache memoryCache;

        static Timer _clearTempDb, _clearHistory;

        static DateTime _nextClearDb = DateTime.Now.AddMinutes(5);

        static readonly ConcurrentDictionary<string, (DateTime extend, bool IsSerialize, DateTime ex, object value)> tempDb = new();

        /// <summary>
        /// key, (ex кеша, <ip, время>)
        /// </summary>
        static readonly ConcurrentDictionary<string, (DateTime ex, ConcurrentDictionary<string, DateTime> requests)> requestHistory = new();

        public static int Stat_ContTempDb => tempDb.IsEmpty ? 0 : tempDb.Count;
        #endregion

        #region Configure
        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
            _clearTempDb = new Timer(ClearTempDb, null, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(200));
            _clearHistory = new Timer(ClearHistory, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }
        #endregion

        #region ClearTempDb
        static int _updatingDb = 0;

        async static void ClearTempDb(object state)
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
                    string[] delete_ids = tempDb.Where(t => now > t.Value.extend)
                        .Select(k => k.Key)
                        .ToArray();

                    if (delete_ids.Length > 0)
                    {
                        using (var sqlDb = new HybridCacheContext())
                        {
                            await sqlDb.files
                                .Where(x => delete_ids.Contains(x.Id))
                                .ExecuteDeleteAsync();

                            foreach (string tempid in delete_ids)
                            {
                                if (tempDb.TryGetValue(tempid, out var c))
                                {
                                    sqlDb.files.Add(new HybridCacheSqlModel()
                                    {
                                        Id = tempid,
                                        ex = c.ex,
                                        value = c.IsSerialize
                                            ? JsonConvertPool.SerializeObject(c.value)
                                            : c.value.ToString(),
                                        capacity = GetCapacity(c.value)
                                    });
                                }
                            }

                            await sqlDb.SaveChangesAsync();

                            foreach (string key in delete_ids)
                                tempDb.TryRemove(key, out _);
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

        #region ClearHistory
        static int _updatingHistory = 0;

        async static void ClearHistory(object state)
        {
            if (requestHistory.IsEmpty)
                return;

            if (Interlocked.Exchange(ref _updatingHistory, 1) == 1)
                return;

            try
            {
                var now = DateTime.Now;
                var cutoff = now.AddSeconds(-60);

                foreach (var history in requestHistory)
                {
                    foreach (var req in history.Value.requests)
                    {
                        if (cutoff > req.Value)
                            history.Value.requests.TryRemove(req.Key, out _);
                    }

                    if (history.Value.requests.Count == 0)
                    {
                        requestHistory.TryRemove(history.Key, out _);
                        continue;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref _updatingHistory, 0);
            }
        }
        #endregion


        #region HybridCache
        RequestModel requestInfo;

        public HybridCache() { }

        public HybridCache(RequestModel requestInfo)
        {
            this.requestInfo = requestInfo;
        }
        #endregion


        #region TryGetValue
        public bool TryGetValue<TItem>(string key, out TItem value, bool? inmemory = null)
        {
            if (AppInit.conf.mikrotik == false && AppInit.conf.cache.type != "mem")
            {
                if (memoryCache.TryGetValue(key, out value))
                    return true;

                if (ReadCache(key, out value))
                    return true;

                return false;
            }

            return memoryCache.TryGetValue(key, out value);
        }
        #endregion

        #region ReadCache
        private bool ReadCache<TItem>(string key, out TItem value)
        {
            value = default;

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
                if (tempDb.TryGetValue(key, out var _temp))
                {
                    value = (TItem)_temp.value;
                    updateRequestHistory(key, _temp.ex, value);
                    return true;
                }
                else
                {
                    using (var sqlDb = HybridCacheContext.Factory?.CreateDbContext() ?? new HybridCacheContext())
                    {
                        using (var conn = sqlDb.Database.GetDbConnection())
                        {
                            conn.Open();

                            using (var cmd = conn.CreateCommand())
                            {
                                cmd.CommandText = "SELECT ex, value, capacity FROM files WHERE Id = $id";
                                var p = cmd.CreateParameter();
                                p.ParameterName = "$id";
                                p.Value = key;
                                cmd.Parameters.Add(p);

                                using (var r = cmd.ExecuteReader())
                                {
                                    if (!r.Read())
                                        return false;

                                    var ex = r.GetDateTime(0);
                                    if (DateTime.Now > ex)
                                        return false;

                                    if (IsDeserialize)
                                    {
                                        bool isCapacity = IsCapacityCollection(type);

                                        int capacity = 0;
                                        if (isCapacity && !r.IsDBNull(2))
                                            capacity = r.GetInt32(2);

                                        using (var textReader = r.GetTextReader(1))
                                        {
                                            using (var jsonReader = new JsonTextReader(textReader)
                                            {
                                                ArrayPool = NewtonsoftPool.Array
                                            })
                                            {
                                                var serializer = _serializer.Value;

                                                if (isCapacity && capacity > 0)
                                                {
                                                    var instance = CreateCollectionWithCapacity(type, capacity);
                                                    if (instance != null)
                                                    {
                                                        serializer.Populate(jsonReader, instance);
                                                        value = (TItem)instance;
                                                    }
                                                    else
                                                    {
                                                        value = serializer.Deserialize<TItem>(jsonReader);
                                                    }
                                                }
                                                else
                                                {
                                                    value = serializer.Deserialize<TItem>(jsonReader);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        value = (TItem)Convert.ChangeType(r.GetString(1), typeof(TItem), CultureInfo.InvariantCulture);
                                    }

                                    updateRequestHistory(key, ex, value);
                                    return true;
                                }
                            }
                        }
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
            if (inmemory != true && AppInit.conf.mikrotik == false && WriteCache(key, value, absoluteExpiration, default))
                return value;

            if (inmemory != true && AppInit.conf.mikrotik == false)
                Console.WriteLine($"set memory: {key} / {DateTime.Now}");

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool? inmemory = null)
        {
            if (inmemory != true && AppInit.conf.mikrotik == false && WriteCache(key, value, default, absoluteExpirationRelativeToNow))
                return value;

            if (inmemory != true && AppInit.conf.mikrotik == false)
                Console.WriteLine($"set memory: {key} / {DateTime.Now}");

            return memoryCache.Set(key, value, absoluteExpirationRelativeToNow);
        }
        #endregion

        #region WriteCache
        private bool WriteCache<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, TimeSpan absoluteExpirationRelativeToNow)
        {
            if (AppInit.conf.cache.type == "mem")
                return false;

            // кеш уже получен от другого rch клиента
            if (tempDb.ContainsKey(key))
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

                tempDb.TryAdd(key, (extend, IsSerialize, absoluteExpiration.DateTime, value));

                return true;
            }
            catch { }

            return false;
        }
        #endregion


        #region updateRequestHistory
        private void updateRequestHistory<TItem>(string key, DateTime ex, TItem value)
        {
            if (AppInit.conf.cache.type != "hybrid" || requestInfo == null)
                return;

            var history = requestHistory.GetOrAdd(key, _ => (ex, new ConcurrentDictionary<string, DateTime>()));
            history.requests.AddOrUpdate(requestInfo.IP, DateTime.Now, (k,v) => DateTime.Now);

            if (history.requests.Count >= 5)
            {
                var timecache = ex > DateTime.Now.AddMinutes(15) 
                    ? DateTime.Now.AddMinutes(10) 
                    : ex; // 1-15

                memoryCache.Set(key, value, timecache);

                requestHistory.TryRemove(key, out _);
                tempDb.TryRemove(key, out _);
            }
        }
        #endregion

        #region collection capacity
        static bool IsCapacityCollection(Type type)
        {
            if (type == typeof(string) || type.IsArray)
                return false;

            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                var def = iface.GetGenericTypeDefinition();
                if (def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
                    return true;
            }

            return false;
        }

        static int GetCapacity(object value)
        {
            if (value is string)
                return 0;

            foreach (var iface in value.GetType().GetInterfaces())
            {
                if (!iface.IsGenericType)
                    continue;

                var def = iface.GetGenericTypeDefinition();

                if (def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
                {
                    var countProperty = iface.GetProperty("Count");

                    if (countProperty?.PropertyType == typeof(int))
                        return (int)countProperty.GetValue(value);
                }
            }

            return 0;
        }

        static object CreateCollectionWithCapacity(Type type, int capacity)
        {
            if (type == typeof(string) || type.IsArray)
                return null;

            var ctor = type.GetConstructor(new[] { typeof(int) });
            if (ctor != null)
                return ctor.Invoke(new object[] { capacity });

            if (type.IsInterface && type.IsGenericType)
            {
                var listType = typeof(List<>).MakeGenericType(type.GetGenericArguments());
                var listCtor = listType.GetConstructor(new[] { typeof(int) });
                if (listCtor != null)
                    return listCtor.Invoke(new object[] { capacity });
            }

            return null;
        }
        #endregion
    }
}
