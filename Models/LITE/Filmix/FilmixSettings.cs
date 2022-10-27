namespace Lampac.Models.LITE.Filmix
{
    public class FilmixSettings
    {
        public FilmixSettings(string host)
        {
            this.host = host;
        }


        public string host { get; set; }

        public string token { get; set; }

        public bool useproxy { get; set; }

        public bool streamproxy { get; set; }
    }
}
