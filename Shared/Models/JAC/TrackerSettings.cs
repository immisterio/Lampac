using Shared.Model.Base;

namespace Lampac.Models.JAC
{
    public class TrackerSettings : Iproxy
    {
        public TrackerSettings(string host, bool enable = true, bool useproxy = false, LoginSettings login = null, string priority = null)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;

            if (login != null)
                this.login = login;

            this.priority = priority;
        }


        public string host { get; set; }

        public bool enable { get; set; }

        public string priority { get; set; }

        public LoginSettings login { get; set; } = new LoginSettings();

        public string cookie { get; set; }


        public bool useproxy { get; set; }

        public bool useproxystream { get; set; }

        public string globalnameproxy { get; set; }

        public ProxySettings proxy { get; set; }
    }
}
