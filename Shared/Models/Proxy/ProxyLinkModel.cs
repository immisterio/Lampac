using System.Net;
using System.Text.Json.Serialization;

namespace Shared.Models.Proxy
{
    public class ProxyLinkModel
    {
        public ProxyLinkModel() 
        { 
            ex = DateTime.Now.AddHours(AppInit.conf.mikrotik ? 4 : 20);
        }

        public ProxyLinkModel(string reqip, List<HeadersModel> headers, WebProxy proxy, string uri, string plugin = null, bool verifyip = true, DateTime ex = default)
        {
            this.ex = ex;
            this.reqip = reqip;
            this.headers = headers;
            this.proxy = proxy;
            this.uri = uri;
            this.plugin = plugin;
            this.verifyip = verifyip;

            if (this.ex == default)
                this.ex = DateTime.Now.AddHours(AppInit.conf.mikrotik ? 4 : 20);
        }

        [JsonIgnore]
        public string id { get; set; }

        [JsonIgnore]
        public DateTime ex { get; set; }

        public string reqip { get; set; }

        public List<HeadersModel> headers { get; set; }

        [JsonIgnore]
        public WebProxy proxy { get; set; }

        public string uri { get; set; }

        public string plugin { get; set; }

        public bool verifyip { get; set; }
    }
}
