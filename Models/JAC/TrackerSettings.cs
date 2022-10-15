namespace Lampac.Models.JAC
{
    public class TrackerSettings
    {
        public TrackerSettings(string host, bool enable, bool useproxy, LoginSettings login = null)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;
            this.login = login;
        }


        public string host { get; set; }

        public bool enable { get; set; }

        public bool useproxy { get; set; }

        public LoginSettings login { get; set; }
    }
}
