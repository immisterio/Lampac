namespace Lampac.Models.LITE
{
    public class BazonSettings
    {
        public BazonSettings(string apihost, string token, bool localip)
        {
            this.apihost = apihost;
            this.token = token;
            this.localip = localip;
        }


        public string? displayname { get; set; }

        public string apihost { get; set; }

        public string token { get; set; }

        public bool localip { get; set; }
    }
}
