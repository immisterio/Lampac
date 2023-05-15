using Shared.Model.Proxy;

namespace Lampac.Models.LITE
{
    public class HDVBSettings : Iproxy
    {
        public HDVBSettings(string apihost, string token)
        {
            this.apihost = apihost;
            this.token = token;
        }


        public string? displayname { get; set; }

        public string apihost { get; set; }

        public string token { get; set; }


        public bool useproxy { get; set; }

        public string? globalnameproxy { get; set; }

        public bool streamproxy { get; set; }

        public ProxySettings? proxy { get; set; }
    }
}
