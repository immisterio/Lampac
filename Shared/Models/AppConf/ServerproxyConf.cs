using Shared.Model.Base;

namespace Lampac.Models.AppConf
{
    public class ServerproxyConf : Iproxy
    {
        public bool enable { get; set; }

        public bool encrypt { get; set; }

        public bool verifyip { get; set; }

        public bool allow_tmdb { get; set; }

        public bool cache_img { get; set; }

        public bool cache_hls { get; set; }

        public bool showOrigUri { get; set; }


        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings? proxy { get; set; }
    }
}
