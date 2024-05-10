using Shared.Model.Base;
using Shared.Model.Online.VoKino;

namespace Lampac.Models.LITE
{
    public class VokinoSettings : BaseSettings
    {
        public VokinoSettings(string host, bool streamproxy, bool rip = false)
        {
            this.host = host;
            this.streamproxy = streamproxy;
            this.rip = rip;
        }


        public string? token { get; set; }

        public ViewOnline online { get; set; } = new ViewOnline();


        public VokinoSettings Clone()
        {
            return (VokinoSettings)MemberwiseClone();
        }
    }
}
