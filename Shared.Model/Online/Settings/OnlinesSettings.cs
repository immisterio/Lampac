using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class OnlinesSettings : BaseSettings
    {
        public OnlinesSettings(string host, string? apihost = null, bool useproxy = false, string? token = null, bool enable = true, bool streamproxy = false, bool rip = false)
        {
            this.host = host;
            this.apihost = apihost;
            this.enable = enable;
            this.token = token;
            this.useproxy = useproxy;
            this.streamproxy = streamproxy;
            this.rip = rip;
        }


        public string? token { get; set; }

        public string? cookie { get; set; }

        public bool hls { get; set; }

        public bool dash { get; set; }

        public OnlinesSettings Clone()
        {
            return (OnlinesSettings)MemberwiseClone();
        }
    }
}
