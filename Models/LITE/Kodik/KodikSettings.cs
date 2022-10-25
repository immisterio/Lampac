namespace Lampac.Models.LITE
{
    public class KodikSettings
    {
        public KodikSettings(string apihost, string linkhost, string token, string secret_token, bool localip)
        {
            this.apihost = apihost;
            this.linkhost = linkhost;
            this.token = token;
            this.secret_token = secret_token;
            this.localip = localip;
        }


        public string apihost { get; set; }

        public string linkhost { get; set; }

        public string token { get; set; }

        public string secret_token { get; set; }

        public bool localip { get; set; }
    }
}
