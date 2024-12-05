using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class LumexSettings : BaseSettings
    {
        public LumexSettings(string apihost, string token, string iframehost, string clientId)
        {
            if (apihost != null)
                this.apihost = apihost.StartsWith("http") ? apihost : Decrypt(apihost);

            if (iframehost != null)
                this.iframehost = (iframehost.Contains(":") || iframehost.Contains("{")) ? Decrypt(iframehost) : iframehost;

            if (token != null)
                this.token = (token.Contains(":") || token.Contains("{")) ? Decrypt(token) : token;

            this.clientId = clientId;
            hls = true;
        }


        public string? token { get; set; }

        public string? clientId { get; set; }

        public string? iframehost { get; set; }

        public bool hls { get; set; }
    }
}
