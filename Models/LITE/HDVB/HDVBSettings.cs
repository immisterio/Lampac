namespace Lampac.Models.LITE.HDVB
{
    public class HDVBSettings
    {
        public HDVBSettings(string apihost, string token)
        {
            this.apihost = apihost;
            this.token = token;
        }


        public string apihost { get; set; }

        public string token { get; set; }
    }
}
