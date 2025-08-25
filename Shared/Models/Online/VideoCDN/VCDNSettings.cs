namespace Shared.Models.Online.VideoCDN
{
    public class VCDNSettings
    {
        public VCDNSettings(string apihost, string token, string cdnhost, bool useproxy)
        {
            this.apihost = apihost;
            this.token = token;
            this.cdnhost = cdnhost;
            this.useproxy = useproxy;
        }


        public string apihost { get; set; }

        public string token { get; set; }

        public string cdnhost { get; set; }

        public bool useproxy { get; set; }

        public bool streamproxy { get; set; }
    }
}
