using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class KinoPubSettings : BaseSettings
    {
        public KinoPubSettings(string? apihost = null)
        {
            this.apihost = apihost;
        }

        public string? token { get; set; }

        public string? filetype { get; set; }


        public bool ssl { get; set; }

        public bool hevc { get; set; }

        public bool hdr { get; set; }

        public bool uhd { get; set; }
    }
}
