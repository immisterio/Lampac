using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class IframeVideoSettings : BaseSettings
    {
        public IframeVideoSettings(string host, string cdnhost, bool enable = true)
        {
            apihost = host;
            this.cdnhost = cdnhost;
            this.enable = enable;
        }

        public string cdnhost { get; set; }

        public string? token { get; set; }
    }
}
