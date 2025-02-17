using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class LumexSettings : BaseSettings
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


        public string? token { get; set; }

        public string? clientId { get; set; }

        public string? iframehost { get; set; }

        public LumexSettings Clone()
        {
            return (LumexSettings)MemberwiseClone();
        }
    }
}
