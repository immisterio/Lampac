namespace Shared.Models.Base
{
    public interface Istreamproxy
    {
        public bool rhub { get; set; }

        public bool rhub_streamproxy { get; set; }

        public bool useproxystream { get; set; }

        public bool streamproxy { get; set; }

        public bool apnstream { get; set; }

        public string[] geostreamproxy { get; set; }

        public bool qualitys_proxy { get; set; }

        public ProxySettings proxy { get; set; }

        public ApnConf apn { get; set; }
    }
}
