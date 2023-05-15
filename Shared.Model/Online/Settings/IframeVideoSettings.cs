using Shared.Model.Proxy;

namespace Lampac.Models.LITE
{
    public class IframeVideoSettings : Iproxy
    {
        public IframeVideoSettings(string host, string cdnhost)
        {
            apihost = host;
            this.cdnhost = cdnhost;
        }


        public string? displayname { get; set; }

        public string? apihost { get; set; }

        public string cdnhost { get; set; }

        public string? token { get; set; }

        public bool enable { get; set; }


        public bool useproxy { get; set; }

        public string? globalnameproxy { get; set; }

        public bool streamproxy { get; set; }

        public ProxySettings? proxy { get; set; }
    }
}
