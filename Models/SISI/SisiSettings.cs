namespace Lampac.Models.SISI
{
    public class SisiSettings
    {
        public SisiSettings(string host, bool enable = true, bool useproxy = false)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public bool enable { get; set; }

        public bool useproxy { get; set; }

        public bool streamproxy { get; set; }
    }
}
