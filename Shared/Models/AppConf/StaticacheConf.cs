namespace Shared.Models.AppConf;

public class StaticacheConf
{
    public bool enable { get; set; }

    public int minimalCacheMinutes { get; set; }

    public List<StaticacheRoute> routes { get; set; } = new List<StaticacheRoute>();

    public string[] disabledPaths { get; set; }
}


public class StaticacheRoute
{
    public StaticacheRoute() { }

    public StaticacheRoute(string pathRex, int cacheMinutes, string[] queryKeys)
    {
        this.pathRex = pathRex;
        this.cacheMinutes = cacheMinutes;
        this.queryKeys = queryKeys;
    }

    public string pathRex { get; set; }

    public int cacheMinutes { get; set; }

    public bool skipUids { get; set; }

    public string[] queryKeys { get; set; }
}
