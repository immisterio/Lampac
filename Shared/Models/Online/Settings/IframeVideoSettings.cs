using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class IframeVideoSettings : BaseSettings, ICloneable
    {
        public IframeVideoSettings(string plugin, string host, string cdnhost, bool enable = true)
        {
            this.cdnhost = cdnhost;
            this.enable = enable;
            this.plugin = plugin;

            if (host != null)
                apihost = host.StartsWith("http") ? host : Decrypt(host);
        }

        public string cdnhost { get; set; }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
