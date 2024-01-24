using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class KodikSettings : BaseSettings
    {
        public KodikSettings(string apihost, string linkhost, string token, string secret_token, bool localip, bool enable = true, bool hls = true, bool streamproxy = false)
        {
            this.apihost = apihost;
            this.linkhost = linkhost;
            this.token = token;
            this.secret_token = secret_token;
            this.localip = localip;
            this.enable = enable;
            this.hls = hls;
            this.streamproxy = streamproxy;
        }


        public string linkhost { get; set; }

        public string token { get; set; }

        public string secret_token { get; set; }

        public bool localip { get; set; }

        public bool hls { get; set; }

        public KodikSettings Clone()
        {
            return (KodikSettings)MemberwiseClone();
        }
    }
}
