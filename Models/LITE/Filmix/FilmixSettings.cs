namespace Lampac.Models.LITE.Filmix
{
    public class FilmixSettings
    {
        public FilmixSettings(string host, bool enable = true)
        {
            this.host = host;
            this.enable = enable;
        }


        public string displayname { get; set; }

        public string host { get; set; }

        public bool enable { get; set; }

        public bool pro { get; set; }

        public string token { get; set; }

        public bool useproxy { get; set; }

        public bool streamproxy { get; set; }
    }
}
