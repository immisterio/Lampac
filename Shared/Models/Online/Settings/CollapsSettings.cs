using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class CollapsSettings : BaseSettings, ICloneable
    {
        public CollapsSettings(string plugin, string host, bool enable = true, bool streamproxy = false, bool two = false)
        {
            this.enable = enable;
            this.plugin = plugin;
            this.streamproxy = streamproxy;
            this.two = two;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public bool two { get; set; }
         
        public bool dash { get; set; }


        public CollapsSettings Clone()
        {
            return (CollapsSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
