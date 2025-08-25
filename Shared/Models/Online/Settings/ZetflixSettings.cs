using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class ZetflixSettings : BaseSettings, ICloneable
    {
        public ZetflixSettings(string plugin, string host, bool enable = true, bool streamproxy = false, bool rip = false)
        {
            this.enable = enable;
            this.plugin = plugin;
            this.streamproxy = streamproxy;
            this.rip = rip;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public bool browser_keepopen { get; set; }

        public ZetflixSettings Clone()
        {
            return (ZetflixSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
