using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class RezkaSettings : BaseSettings
    {
        public RezkaSettings(string host, bool streamproxy = false)
        {
            enable = true;
            this.host = host;
            this.streamproxy = streamproxy;
        }


        public string? login { get; set; }

        public string? passwd { get; set; }

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
