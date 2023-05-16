using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class FilmixSettings : BaseSettings
    {
        public FilmixSettings(string host, bool enable = true)
        {
            this.host = host;
            this.enable = enable;
        }


        public bool pro { get; set; }

        public string? token { get; set; }


        public string? APIKEY { get; set; }

        public string? APISECRET { get; set; }

        public string? user_name { get; set; }

        public string? user_passw { get; set; }
    }
}
