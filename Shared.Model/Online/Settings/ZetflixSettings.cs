using Shared.Model.Base;

namespace Shared.Model.Online.Settings
{
    public class ZetflixSettings : BaseSettings
    {
        public ZetflixSettings(string host, bool enable = true, bool streamproxy = false, bool rip = false)
        {
            this.enable = enable;
            this.streamproxy = streamproxy;
            this.rip = rip;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public bool hls { get; set; }

        public bool black_magic { get; set; }

        public ZetflixSettings Clone()
        {
            return (ZetflixSettings)MemberwiseClone();
        }
    }
}
