namespace Shared.Services.Hybrid;

public record HybridCacheEntry<T>(bool success, T value, bool singleCache);

public class BaseHybridCache
{
    public static readonly Serilog.ILogger Log = Serilog.Log.ForContext<BaseHybridCache>();

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, CollectionMetadata> CollectionMetadataCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Func<object, int>> CountReaderCache = new();

    private record CollectionMetadata(bool IsCapacityCollection, Func<int, object> CapacityFactory);

    private static CollectionMetadata GetCollectionMetadata(Type type)
        => CollectionMetadataCache.GetOrAdd(type, BuildCollectionMetadata);

    private static CollectionMetadata BuildCollectionMetadata(Type type)
    {
        if (type == typeof(string) || type.IsArray)
            return new CollectionMetadata(false, null);

        try
        {
            var isCapacityCollection = false;

            if (type.IsGenericType)
            {
                var genericTypeDefinition = type.GetGenericTypeDefinition();
                if (genericTypeDefinition == typeof(ICollection<>) || genericTypeDefinition == typeof(IReadOnlyCollection<>))
                    isCapacityCollection = true;
            }

            if (!isCapacityCollection)
            {
                foreach (var iface in type.GetInterfaces())
                {
                    if (!iface.IsGenericType)
                        continue;

                    var def = iface.GetGenericTypeDefinition();
                    if (def == typeof(ICollection<>) || def == typeof(IReadOnlyCollection<>))
                    {
                        isCapacityCollection = true;
                        break;
                    }
                }
            }

            if (!isCapacityCollection)
                return new CollectionMetadata(false, null);

            if (typeof(Newtonsoft.Json.Linq.JToken).IsAssignableFrom(type))
                return new CollectionMetadata(true, null);

            var ctor = type.GetConstructor(new[] { typeof(int) });
            if (ctor != null)
                return new CollectionMetadata(true, capacity => ctor.Invoke(new object[] { capacity }));

            if (type.IsInterface && type.IsGenericType)
            {
                var listType = typeof(List<>).MakeGenericType(type.GetGenericArguments());
                var listCtor = listType.GetConstructor(new[] { typeof(int) });
                if (listCtor != null)
                    return new CollectionMetadata(true, capacity => listCtor.Invoke(new object[] { capacity }));
            }

            return new CollectionMetadata(true, null);
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_bi2f8k03");
        }

        return new CollectionMetadata(false, null);
    }

    protected static bool IsCapacityCollection(Type type)
    {
        return GetCollectionMetadata(type).IsCapacityCollection;
    }

    protected static int GetCapacity(object value)
    {
        if (value is string)
            return 0;

        try
        {
            var countReader = CountReaderCache.GetOrAdd(value.GetType(), BuildCountReader);
            return countReader(value);
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_rxx4nf0k");
        }

        return 0;
    }

    private static Func<object, int> BuildCountReader(Type type)
    {
        try
        {
            var collectionInterface = ResolveCollectionInterface(type);
            if (collectionInterface == null)
                return _ => 0;

            var countProperty = collectionInterface.GetProperty("Count");
            if (countProperty?.PropertyType != typeof(int))
                return _ => 0;

            var valueParameter = System.Linq.Expressions.Expression.Parameter(typeof(object), "value");
            var castValue = System.Linq.Expressions.Expression.Convert(valueParameter, collectionInterface);
            var propertyAccess = System.Linq.Expressions.Expression.Property(castValue, countProperty);
            var lambda = System.Linq.Expressions.Expression.Lambda<Func<object, int>>(propertyAccess, valueParameter);

            return lambda.Compile();
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_0x6r2k1l");
        }

        return _ => 0;
    }

    private static Type ResolveCollectionInterface(Type type)
    {
        if (type.IsGenericType)
        {
            var typeDefinition = type.GetGenericTypeDefinition();
            if (typeDefinition == typeof(ICollection<>) || typeDefinition == typeof(IReadOnlyCollection<>))
                return type;
        }

        foreach (var iface in type.GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            var ifaceDefinition = iface.GetGenericTypeDefinition();
            if (ifaceDefinition == typeof(ICollection<>) || ifaceDefinition == typeof(IReadOnlyCollection<>))
                return iface;
        }

        return null;
    }

    protected static object CreateCollectionWithCapacity(Type type, int capacity)
    {
        try
        {
            var metadata = GetCollectionMetadata(type);
            if (!metadata.IsCapacityCollection || metadata.CapacityFactory == null)
                return null;

            return metadata.CapacityFactory(capacity);
        }
        catch (System.Exception ex)
        {
            Log.Error(ex, "CatchId={CatchId}", "id_ct40hhah");
        }

        return null;
    }
}
