using Shared.Model.Base;

namespace Shared.Models.ServerProxy
{
    public class ServerproxyTmdb : Iproxy
    {
        public string API_IP { get; set; }

        public string IMG_IP { get; set; }


        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }
    }
}
