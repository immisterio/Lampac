using System.Net;
using System;
using System.Collections.Generic;

namespace Shared.Models
{
    public class ProxyLinkModel
    {
        public ProxyLinkModel(string reqip, List<(string name, string val)> headers, WebProxy proxy, string uri)
        {
            this.upd = DateTime.Now;
            this.reqip = reqip;
            this.headers = headers;
            this.proxy = proxy;
            this.uri = uri;
        }

        public DateTime upd { get; set; }

        public string reqip { get; set; }

        public List<(string name, string val)> headers { get; set; }

        public WebProxy proxy { get; set; }

        public string uri { get; set; }
    }
}
