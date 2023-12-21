using Shared.Model.Base;

namespace Lampac.Models.SISI
{
    public class SisiSettings : BaseSettings
    {
        public SisiSettings(string host, bool enable = true, bool useproxy = false, bool streamproxy = false)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;
            this.streamproxy = streamproxy;
        }

        public string? cookie { get; set; }

        public SisiSettings Clone()
        {
            return (SisiSettings)MemberwiseClone();
        }
    }
}
