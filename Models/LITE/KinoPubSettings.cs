namespace Lampac.Models.LITE
{
    public class KinoPubSettings
    {
        public KinoPubSettings(string apihost = null)
        {
            this.apihost = apihost;
        }


        public string apihost { get; set; }

        public string token { get; set; }

        public string filetype { get; set; }

        public bool streamproxy { get; set; }
    }
}
