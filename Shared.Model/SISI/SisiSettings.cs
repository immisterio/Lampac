using Shared.Model;
using Shared.Model.Proxy;

namespace Lampac.Models.SISI
{
    public class SisiSettings : Iproxy
    {
        public SisiSettings(string host, bool enable = true, bool useproxy = false)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public bool enable { get; set; }


        public bool useproxy { get; set; }

        public string? globalnameproxy { get; set; }

        public bool streamproxy { get; set; }

        public ProxySettings? proxy { get; set; }


        public bool corseu { get; set; }

        public string? webcorshost { get; set; }

        public string corsHost()
        {
            string? crhost = !string.IsNullOrWhiteSpace(webcorshost) ? webcorshost : corseu ? AppInit.corseuhost : null;
            if (string.IsNullOrWhiteSpace(crhost))
                return host;

            return $"{crhost}/{host}";
        }

        public string corsHost(string uri)
        {
            string? crhost = !string.IsNullOrWhiteSpace(webcorshost) ? webcorshost : corseu ? AppInit.corseuhost : null;
            if (string.IsNullOrWhiteSpace(crhost) || string.IsNullOrWhiteSpace(uri) || uri.Contains(crhost))
                return uri;

            return $"{crhost}/{uri}";
        }
    }
}
