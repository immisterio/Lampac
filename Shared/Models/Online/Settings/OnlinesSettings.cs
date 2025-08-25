using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class OnlinesSettings : BaseSettings, ICloneable
    {
        public OnlinesSettings(string plugin, string host, string apihost = null, bool useproxy = false, string token = null, bool enable = true, bool streamproxy = false, bool rip = false, bool forceEncryptToken = false)
        {
            this.enable = enable;
            this.plugin = plugin;
            this.useproxy = useproxy;
            this.streamproxy = streamproxy;
            this.rip = rip;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);

            if (apihost != null)
                this.apihost = apihost.StartsWith("http") ? apihost : Decrypt(apihost);

            if (token != null)
                this.token = forceEncryptToken || token.Contains(":") || token.Contains("<") ? Decrypt(token) : token;
        }

        public bool imitationHuman { get; set; }


        public OnlinesSettings Clone()
        {
            return (OnlinesSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
