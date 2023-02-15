namespace Lampac.Models.LITE
{
    public class HDVBSettings
    {
        public HDVBSettings(string apihost, string token)
        {
            this.apihost = apihost;
            this.token = token;
        }


        public string displayname { get; set; }

        public string apihost { get; set; }

        public string token { get; set; }
    }
}
