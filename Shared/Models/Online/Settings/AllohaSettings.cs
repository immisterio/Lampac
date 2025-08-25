using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class AllohaSettings : BaseSettings, ICloneable
    {
        public AllohaSettings(string plugin, string apihost, string linkhost, string token, string secret_token, bool localip, bool m4s)
        {
            this.plugin = plugin;
            this.token = token;
            this.secret_token = secret_token;
            this.localip = localip;
            this.m4s = m4s;

            this.linkhost = linkhost == null ? string.Empty : (linkhost.StartsWith("http") ? linkhost : Decrypt(linkhost)!);
            this.apihost = apihost == null ? string.Empty : (apihost.StartsWith("http") ? apihost : Decrypt(apihost));
        }


        public string secret_token { get; set; }

        public string linkhost { get; set; }

        public bool localip { get; set; }

        public bool m4s { get; set; }

        public bool reserve { get; set; }


        public AllohaSettings Clone()
        {
            return (AllohaSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
