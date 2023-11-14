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


        public string? cookie { get; set; }

        public bool xrealip { get; set; }

        public bool xapp { get; set; }
    }
}
