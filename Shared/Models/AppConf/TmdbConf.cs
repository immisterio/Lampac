using Shared.Models.Base;

namespace Shared.Models.AppConf
{
    public class TmdbConf : Iproxy
    {
        public bool enable { get; set; }

        public int httpversion { get; set; }

        public string api_key { get; set; }


        public string DNS { get; set; }

        public int DNS_TTL { get; set; }


        public string API_IP { get; set; }

        public string API_Minor { get; set; }


        public string IMG_IP { get; set; }

        public string IMG_Minor { get; set; }


        public int cache_api { get; set; }

        public int cache_img { get; set; }

        public bool check_img { get; set; }


        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }
    }
}
