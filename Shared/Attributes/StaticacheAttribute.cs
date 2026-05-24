namespace Shared.Attributes;

public record StatiCacheEntry(DateTimeOffset ex);


[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class StaticacheAttribute : Attribute
{
    /// <param name="cacheMinutes">
    /// Если в ответе не будет точного времени, будет использовано это значение
    /// При использовании InvokeCache/InvokeCacheResult система самостоятельно возвращает нужное время
    /// </param>
    /// <param name="manually">
    /// Ручная настройка через routes
    /// Выигрыш в кеше сомнителен или есть сложности (привязка к ip и т.д)
    /// </param>
    /// <param name="always">
    /// Кеширует на уровне системы даже если в init отключён
    /// </param>
    public StaticacheAttribute(int cacheMinutes = 1, bool manually = false, bool always = false, bool setHeadersNoCache = false)
    {
        if (0 >= cacheMinutes)
            cacheMinutes = 1;

        this.cacheMinutes = cacheMinutes;
        this.manually = manually;
        this.always = always;
        this.setHeadersNoCache = setHeadersNoCache;
    }

    public int cacheMinutes { get; }

    public bool manually { get; }

    public bool always { get; }

    public bool setHeadersNoCache { get; }
}
