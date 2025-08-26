using Shared.Models.Base;

namespace Shared.Models.Online.Settings
{
    public class KinobaseSettings : BaseSettings
    {
        public KinobaseSettings(string plugin, string host, bool playerjs, bool hdr)
        {
            enable = true;
            this.plugin = plugin;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);

            this.playerjs = playerjs;
            this.hdr = hdr;
        }


        public bool playerjs { get; set; }

        public bool hdr { get; set; }
    }
}
