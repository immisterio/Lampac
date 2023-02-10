namespace Lampac.Models.LITE
{
    public class IframeVideoSettings
    {
        public IframeVideoSettings(string host, string cdnhost)
        {
            apihost = host;
            this.cdnhost = cdnhost;
        }


        public string displayname { get; set; }

        public string apihost { get; set; }

        public string cdnhost { get; set; }

        public string token { get; set; }

        public bool enable { get; set; }

        public bool useproxy { get; set; }

        public bool streamproxy { get; set; }
    }
}
