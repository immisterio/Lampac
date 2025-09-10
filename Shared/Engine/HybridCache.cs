using LiteDB;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;

namespace Shared.Engine
{
    public struct HybridCacheModel()
    {
        [BsonId]
        public string id { get; set; }

        public DateTimeOffset ex { get; set; }

        public string value { get; set; }
    }


    public struct HybridCache
    {
        #region HybridCache
        static IMemoryCache memoryCache;

        public static void Configure(IMemoryCache mem)
        {
            memoryCache = mem;
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
                    if (inmemory != false && AppInit.conf.cache.type == "hybrid")
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
            bool isValueType = type.IsValueType;

            if (!isText && !isConstructor && !isValueType)
                return false;

            try
            {
                var doc = CollectionDb.hybrid_cache.FindById(CrypTo.md5(key));

                if (doc.id == null || DateTimeOffset.Now > doc.ex)
                    return false;

                var eventResult = InvkEvent.HybridCache("read", key, doc.value, doc.ex);

                if (isConstructor || isValueType)
                    value = JsonConvert.DeserializeObject<TItem>(eventResult.value ?? doc.value);
                else
                    value = (TItem)Convert.ChangeType(eventResult.value ?? doc.value, type);

                return true;
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
                if (AppInit.conf.cache.type == "hybrid" && inmemory != false)
                    memoryCache.Set(key, value, DateTime.Now.AddSeconds(AppInit.conf.cache.extend));

                return value;
            }

            return memoryCache.Set(key, value, absoluteExpiration);
        }

        public TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool? inmemory = null)
        {
            if (inmemory != true && !AppInit.conf.mikrotik && WriteCache(key, value, default, absoluteExpirationRelativeToNow))
            {
                if (AppInit.conf.cache.type == "hybrid" && inmemory != false)
                    memoryCache.Set(key, value, DateTime.Now.AddSeconds(AppInit.conf.cache.extend));

                return value;
            }

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
            bool isValueType = type.IsValueType;

            if (!isText && !isConstructor && !isValueType)
                return false;

            try
            {
                string result;

                if (isConstructor || isValueType)
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

                try
                {
                    CollectionDb.hybrid_cache.Insert(new HybridCacheModel()
                    {
                        id = CrypTo.md5(key),
                        ex = absoluteExpiration,
                        value = result
                    });
                }
                catch 
                {
                    var doc = CollectionDb.hybrid_cache.FindById(CrypTo.md5(key));
                    if (doc.id != null)
                    {
                        doc.ex = absoluteExpiration;
                        doc.value = result;
                        CollectionDb.hybrid_cache.Update(doc);
                    }
                }

                return true;
            }
            catch { }

            return false;
        }
        #endregion
    }
}
