using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class KinoPubSettings : BaseSettings
    {
        public KinoPubSettings(string? host = null)
        {
            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }

        public string? token { get; set; }

        public string[]? tokens { get; set; }

        public string? filetype { get; set; }


        public bool ssl { get; set; }

        public bool hevc { get; set; }

        public bool hdr { get; set; }

        public bool uhd { get; set; }

        public KinoPubSettings Clone()
        {
            return (KinoPubSettings)MemberwiseClone();
        }
    }
}
