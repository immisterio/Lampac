using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class KodikSettings : BaseSettings, ICloneable
    {
        public KodikSettings(string plugin, string apihost, string linkhost, string token, string secret_token, bool localip, bool enable = true, bool hls = true, bool streamproxy = false)
        {
            this.plugin = plugin;
            this.secret_token = secret_token;
            this.localip = localip;
            this.enable = enable;
            this.hls = hls;
            this.streamproxy = streamproxy;

            this.linkhost = linkhost.StartsWith("http") ? linkhost : Decrypt(linkhost)!;
            this.apihost = apihost.StartsWith("http") ? apihost : Decrypt(apihost);
            this.token = token.Contains(":") ? Decrypt(token)! : token;
        }


        public string? secret_token { get; set; }

        public string linkhost { get; set; }

        public bool localip { get; set; }

        public KodikSettings Clone()
        {
            return (KodikSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
