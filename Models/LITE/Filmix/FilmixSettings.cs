namespace Lampac.Models.LITE.Filmix
{
    public class FilmixSettings
    {
        public FilmixSettings(string apihost, bool useproxy)
        {
            this.apihost = apihost;
            this.useproxy = useproxy;
        }


        public string apihost { get; set; }

        public bool useproxy { get; set; }
    }
}
