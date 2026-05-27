using Newtonsoft.Json;

namespace Shared.Models.AppConf;

public class WafConf
{
    public bool enable { get; set; }

    public WafDisabled disabled { get; set; }

    public bool allowExternalIpAccessToLocalRequest { get; set; }

    public bool bypassLocalIP { get; set; }

    public bool bruteForceProtection { get; set; }

    public List<string> whiteIps { get; set; }

    public int limit_req { get; set; }

    [JsonProperty("limit_map", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
    public List<WafLimitRootMap> limit_map { get; set; }

    public List<string> ipsDeny { get; set; }

    public List<string> ipsAllow { get; set; }

    public List<string> countryDeny { get; set; }

    public List<string> countryAllow { get; set; }

    public List<WafAsnRange> asnsDeny { get; set; }

    public List<long> asnDeny { get; set; }

    public List<long> asnAllow { get; set; }

    [JsonProperty("headersDeny", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
    public Dictionary<string, string> headersDeny { get; set; }
}

public struct WafDisabled
{
    public bool bruteForceProtection { get; set; }

    public bool country { get; set; }

    public bool asn { get; set; }

    public bool asns { get; set; }

    public bool ips { get; set; }

    public bool limit_req { get; set; }

    public bool limit_map { get; set; }

    public bool headers { get; set; }

    [JsonProperty("heders")]
    public bool heders
    {
        get => headers;
        set => headers = value;
    }
}


public class WafLimitRootMap
{
    public WafLimitRootMap() { }

    public WafLimitRootMap(string pattern, WafLimitMap map)
    {
        this.pattern = pattern;
        this.map = map;
    }

    public string path { get; set; }

    public string pattern { get; set; }

    public WafLimitMap map { get; set; }
}

public class WafLimitMap
{
    public int limit { get; set; }

    public int second { get; set; }

    public bool pathId { get; set; }

    public string[] queryIds { get; set; }
}

public class WafAsnRange
{
    public long start { get; set; }

    public long end { get; set; }
}
