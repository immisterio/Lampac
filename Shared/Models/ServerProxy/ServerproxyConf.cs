using Shared.Models.Base;

namespace Shared.Models.ServerProxy
{
    public class ServerproxyConf : Iproxy
    {
        public bool enable { get; set; }

        public bool encrypt { get; set; }

        public bool encrypt_aes { get; set; }

        public bool verifyip { get; set; }

        public bool showOrigUri { get; set; }

        public ServerproxyImageConf image { get; set; } = new ServerproxyImageConf();


        public bool forced_apn { get; set; }

        public ServerproxyBufferingConf buffering { get; set; } = new ServerproxyBufferingConf();

        public int maxlength_m3u { get; set; }

        public int maxlength_ts { get; set; }


        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }
    }
}
