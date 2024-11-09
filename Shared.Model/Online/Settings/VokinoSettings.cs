using Shared.Model.Base;
using Shared.Model.Online.VoKino;

namespace Lampac.Models.LITE
{
    public class VokinoSettings : BaseSettings
    {
        public VokinoSettings(string host, bool streamproxy, bool rip = false)
        {
            this.streamproxy = streamproxy;
            this.rip = rip;

            if (host != null)
                this.host = host.StartsWith("http") ? host : Decrypt(host);
        }


        public string? token { get; set; }

        public ViewOnline online { get; set; } = new ViewOnline();


        public VokinoSettings Clone()
        {
            return (VokinoSettings)MemberwiseClone();
        }
    }
}
