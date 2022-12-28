namespace Lampac.Models.AppConf
{
    public class WebConf
    {
        public bool autoupdate { get; set; }

        public string index { get; set; }

        public InitPlugins initPlugins = new InitPlugins() { dlna = true, tracks = true, tmdbProxy = true, online = true, sisi = true };
    }
}
