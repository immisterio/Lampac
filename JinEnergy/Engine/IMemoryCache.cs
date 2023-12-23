using System.Text.Json;

namespace JinEnergy.Engine
{
    public static class IMemoryCache
    {
        static long lastid = 0;

        static Dictionary<string, string> cache = new Dictionary<string, string>();

        public static T? Read<T>(long id, string key)
        {
            if (id != 0 && id != lastid)
            {
                lastid = id;
                cache.Clear();
            }

            if (!cache.TryGetValue(key, out string? val))
                return default;

            return JsonSerializer.Deserialize<T>(val);
        }

        public static void Set(string key, object ob)
        {
            cache.TryAdd(key, JsonSerializer.Serialize(ob));
        }

        public static void Remove(string key)
        {
            if (cache.ContainsKey(key))
                cache.Remove(key);
        }

        public static void RemoveAll(string key)
        {
            foreach (var item in cache.ToArray())
            {
                if (item.Key.Contains(key))
                    cache.Remove(key);
            }
        }
    }
}
