namespace Lampac.Models.LITE.Filmix
{
    public class FilmixSettings
    {
        public FilmixSettings(string host, bool useproxy)
        {
            this.host = host;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public bool useproxy { get; set; }
    }
}
