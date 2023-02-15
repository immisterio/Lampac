namespace Lampac.Models.LITE
{
    public class KinoPubSettings
    {
        public KinoPubSettings(string apihost = null)
        {
            this.apihost = apihost;
        }

        public string displayname { get; set; }

        public string apihost { get; set; }

        public string token { get; set; }

        public string filetype { get; set; }

        public bool streamproxy { get; set; }


        public bool ssl { get; set; }

        public bool hevc { get; set; }

        public bool hdr { get; set; }

        public bool uhd { get; set; }
    }
}
