using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class LumexSettings : BaseSettings, ICloneable
    {
        public LumexSettings(string plugin, string apihost, string token, string iframehost, string clientId)
        {
            this.plugin = plugin;

            if (apihost != null)
                this.apihost = apihost.StartsWith("http") ? apihost : Decrypt(apihost);

            if (iframehost != null)
                this.iframehost = iframehost.StartsWith("http") ? iframehost : (iframehost.Contains("{") ? Decrypt(iframehost) : iframehost);

            if (token != null)
                this.token = (token.Contains(":") || token.Contains("{")) ? Decrypt(token) : token;

            this.clientId = clientId;
        }


        public string clientId { get; set; }

        public string iframehost { get; set; }


        public string username { get; set; }

        public string password { get; set; }

        public string domain { get; set; }

        public bool disable_protection { get; set; }

        public bool disable_ads { get; set; }

        public bool log { get; set; }

        public bool verifyip { get; set; }


        public LumexSettings Clone()
        {
            return (LumexSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
