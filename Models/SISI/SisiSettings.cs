namespace Lampac.Models.SISI
{
    public class SisiSettings
    {
        public SisiSettings(string host, bool enable, bool useproxy)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public bool enable { get; set; }

        public bool useproxy { get; set; }
    }
}
