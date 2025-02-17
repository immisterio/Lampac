using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class KinoPubSettings : BaseSettings
    {
        public KinoPubSettings(string plugin, string? host = null)
        {
            this.plugin = plugin;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }

        public string? token { get; set; }

        public string[]? tokens { get; set; }

        public string? filetype { get; set; }

        public KinoPubSettings Clone()
        {
            return (KinoPubSettings)MemberwiseClone();
        }
    }
}
