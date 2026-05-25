using System.Text.RegularExpressions;

namespace Shared.Models.AppConf;

public class StaticacheConf
{
    public bool enable { get; set; }

    /// <summary>
    /// только то что явно указано в routes
    /// </summary>
    public bool manually { get; set; }

    public int minimalCacheMinutes { get; set; }

    public List<StaticacheRoute> routes { get; set; } = new();

    public string[] disabledPaths { get; set; }
}

public struct StaticacheRoute
{
    public string path { get; set; }

    public string pathRex { get; set; }

    public int cacheMinutes { get; set; }

    public bool skipUids { get; set; }

    public string[] queryKeys { get; set; }

    public string[] ignoreQueryKeys { get; set; }
}

public class StaticachePreparedRoute
{
    public StaticacheRoute Route { get; init; }

    public Regex PathRegex { get; init; }
}

public readonly record struct StaticacheCacheModel(long ex, string ext, short statusCode = 200, int contentLength = 0);

public record StaticacheFeature(int cacheMinutes, string cachekey);