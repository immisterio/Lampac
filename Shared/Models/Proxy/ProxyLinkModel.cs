using LiteDB;
using System.Net;

namespace Shared.Models.Proxy
{
    public class ProxyLinkModel
    {
        public ProxyLinkModel() 
        { 
            ex = DateTimeOffset.Now.AddHours(AppInit.conf.mikrotik ? 4 : 36);
        }

        public ProxyLinkModel(string reqip, List<HeadersModel> headers, WebProxy proxy, string uri, string plugin = null, bool verifyip = true, DateTimeOffset ex = default)
        {
            this.ex = ex;
            this.reqip = reqip;
            this.headers = headers;
            this.proxy = proxy;
            this.uri = uri;
            this.plugin = plugin;
            this.verifyip = verifyip;

            if (this.ex == default)
                this.ex = DateTimeOffset.Now.AddHours(AppInit.conf.mikrotik ? 4 : 36);
        }

        [BsonId]
        public string Id { get; set; }

        public DateTimeOffset ex { get; set; }

        public string reqip { get; set; }

        public List<HeadersModel> headers { get; set; }

        [BsonIgnore]
        public WebProxy proxy { get; set; }

        public string uri { get; set; }

        public string plugin { get; set; }

        public bool verifyip { get; set; }
    }
}
