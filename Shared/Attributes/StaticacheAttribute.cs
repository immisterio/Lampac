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
    public StaticacheAttribute(int cacheMinutes = 1, bool manually = false)
    {
        if (0 >= cacheMinutes)
            cacheMinutes = 1;

        this.cacheMinutes = cacheMinutes;
        this.manually = manually;
    }

    public int cacheMinutes { get; }

    public bool manually { get; }
}
