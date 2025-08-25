using Shared.Models.Base;

namespace Shared.Models.SISI.Base
{
    public class SisiSettings : BaseSettings, ICloneable
    {
        public SisiSettings(string plugin, string host, bool enable = true, bool useproxy = false, bool streamproxy = false)
        {
            this.enable = enable;
            this.plugin = plugin;
            this.useproxy = useproxy;
            this.streamproxy = streamproxy;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }

        public SisiSettings Clone()
        {
            return (SisiSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
