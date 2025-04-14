using System.Net;
using System;
using System.Collections.Generic;
using Shared.Model.Online;

namespace Shared.Models
{
    public class ProxyLinkModel
    {
        public ProxyLinkModel(string reqip, List<HeadersModel> headers, WebProxy proxy, string uri, string plugin = null, bool verifyip = true, DateTime ex = default)
        {
            this.ex = ex;
            this.reqip = reqip;
            this.headers = headers;
            this.proxy = proxy;
            this.uri = uri;
            this.plugin = plugin;
            this.verifyip = verifyip;
        }

        public DateTime upd { get; set; } = DateTime.Now;

        public DateTime ex { get; set; }

        public string reqip { get; set; }

        public List<HeadersModel> headers { get; set; }

        public WebProxy proxy { get; set; }

        public string uri { get; set; }

        public string plugin { get; set; }

        public bool verifyip { get; set; }
    }
}
