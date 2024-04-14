using Shared.Model.Base;

namespace Shared.Model.Online.Settings
{
    public class ZetflixSettings : BaseSettings
    {
        public ZetflixSettings(string host, bool enable = true, bool streamproxy = false, bool rip = false)
        {
            this.host = host;
            this.enable = enable;
            this.streamproxy = streamproxy;
            this.rip = rip;
        }


        public bool hls { get; set; }

        public bool black_magic { get; set; }

        public ZetflixSettings Clone()
        {
            return (ZetflixSettings)MemberwiseClone();
        }
    }
}
