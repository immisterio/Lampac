using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class AllohaSettings : BaseSettings
    {
        public AllohaSettings(string apihost, string linkhost, string token, string secret_token, bool localip, bool m4s)
        {
            this.apihost = apihost;
            this.linkhost = linkhost;
            this.token = token;
            this.secret_token = secret_token;
            this.localip = localip;
            this.m4s = m4s;
        }


        public string linkhost { get; set; }

        public string token { get; set; }

        public string secret_token { get; set; }

        public bool localip { get; set; }

        public bool m4s { get; set; }
    }
}
