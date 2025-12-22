using Shared.Models.Base;

namespace Shared.Models.SISI.Base
{
    public class SisiSettings : BaseSettings, ICloneable
    {
        public SisiSettings(string plugin, string host, bool enable = true, bool useproxy = false, bool streamproxy = false, string rch_access = null, string stream_access = null)
        {
            this.enable = enable;
            this.plugin = plugin;
            this.useproxy = useproxy;
            this.streamproxy = streamproxy;
            this.qualitys_proxy = true;
            this.rch_access = rch_access;
            this.stream_access = stream_access;

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
