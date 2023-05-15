using Shared.Model;
using Shared.Model.Proxy;

namespace Lampac.Models.LITE
{
    public class OnlinesSettings : Iproxy
    {
        public OnlinesSettings(string host, string? apihost = null, bool useproxy = false, string? token = null, bool enable = true)
        {
            this.host = host;
            this.apihost = apihost;
            this.enable = enable;
            this.token = token;
            this.useproxy = useproxy;
        }


        public string? displayname { get; set; }

        public string host { get; set; }

        public string? apihost { get; set; }

        public bool enable { get; set; }

        public string? token { get; set; }

        public string? сookie { get; set; }


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
