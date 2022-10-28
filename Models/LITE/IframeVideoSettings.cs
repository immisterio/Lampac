namespace Lampac.Models.LITE
{
    public class IframeVideoSettings
    {
        public IframeVideoSettings(string host)
        {
            apihost = host;
        }


        public string apihost { get; set; }

        public string cdnhost { get; set; }

        public string token { get; set; }

        public bool streamproxy { get; set; }
    }
}
