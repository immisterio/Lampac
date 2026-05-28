using System.Text.Json.Serialization.Metadata;

namespace Shared.Services.Hybrid;

public interface IHybridCache
{
    bool TryGetValue<TItem>(string key, out TItem value, JsonTypeInfo<TItem> jsonType = null, bool textJson = false);


    bool ContainsKey<T>(string key, out T value);

    bool ContainsKey<T>(string key, out T value, out DateTimeOffset ex);

    ValueTask<HybridCacheEntry<TItem>> EntryAsync<TItem>(string key, JsonTypeInfo<TItem> jsonType = null, bool textJson = false);

    Task<(bool succes, TItem value, bool singleCache)> ReadCacheAsync<TItem>(string key, bool fileCache, JsonTypeInfo<TItem> jsonType, bool textJson = false);


    TItem Set<TItem>(string key, TItem value, DateTimeOffset absoluteExpiration, bool? inmemory = null, bool textJson = false);

    TItem Set<TItem>(string key, TItem value, TimeSpan absoluteExpirationRelativeToNow, bool? inmemory = null, bool textJson = false);
}
