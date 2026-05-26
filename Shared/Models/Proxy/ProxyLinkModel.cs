using System.Net;

namespace Shared.Models.Proxy;

public class ProxyLinkModel
{
    public ProxyLinkModel()
    {
        ex = DateTime.UtcNow.AddDays(1);
    }

    public ProxyLinkModel(string reqip, IReadOnlyList<HeadersModel> headers, WebProxy proxy, string uri, string plugin = null, bool verifyip = true, DateTime ex = default, object userdata = null, ulong? bucketHeaders = null)
    {
        this.ex = ex;
        this.reqip = reqip;
        this.headers = headers;
        this.proxy = proxy;
        this.userdata = userdata;
        this.uri = uri;
        this.plugin = plugin;
        this.verifyip = verifyip;
        this.bucketHeaders = bucketHeaders;

        if (this.ex == default)
            this.ex = DateTime.UtcNow.AddDays(1);
    }

    public DateTime ex { get; set; }

    public string reqip { get; set; }

    public IReadOnlyList<HeadersModel> headers { get; set; }

    public WebProxy proxy { get; set; }

    public object userdata { get; set; }

    public ulong? bucketHeaders { get; set; }

    public string uri { get; set; }

    public string plugin { get; set; }

    public bool verifyip { get; set; }

    public bool md5 { get; set; }
}
