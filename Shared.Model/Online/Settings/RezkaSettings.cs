using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class RezkaSettings : BaseSettings
    {
        public RezkaSettings(string host, bool streamproxy = false)
        {
            enable = true;
            this.streamproxy = streamproxy;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public string? login { get; set; }

        public string? passwd { get; set; }

        public bool premium { get; set; }

        public string? cookie { get; set; }

        public string? uacdn { get; set; }

        public bool forceua { get; set; }

        public bool xrealip { get; set; }

        public bool xapp { get; set; }

        public bool hls { get; set; }

        public RezkaSettings Clone()
        {
            return (RezkaSettings)MemberwiseClone();
        }
    }
}
