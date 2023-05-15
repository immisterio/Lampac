using Shared.Model.Proxy;

namespace Lampac.Models.AppConf
{
    public class ServerproxyConf : Iproxy
    {
        public bool enable { get; set; }

        public bool encrypt { get; set; }

        public bool allow_tmdb { get; set; }


        public bool useproxy { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings? proxy { get; set; }
    }
}
