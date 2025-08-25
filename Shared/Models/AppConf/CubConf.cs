using Shared.Models.Base;

namespace Shared.Models.AppConf
{
    public class CubConf : Iproxy
    {
        public bool enable { get; set; }

        public string[] geo { get; set; }

        public bool viewru { get; set; }


        public string domain { get; set; }

        public string scheme { get; set; }

        public string mirror { get; set; }


        public int cache_api { get; set; }

        public int cache_img { get; set; }


        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }


        public bool enabled(string country)
        {
            bool cubproxy = enable;
            if (cubproxy && geo != null)
                cubproxy = geo.Contains(country);

            return cubproxy;
        }
    }
}
