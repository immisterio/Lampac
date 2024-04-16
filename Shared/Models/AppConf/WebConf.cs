namespace Lampac.Models.AppConf
{
    public class WebConf
    {
        public bool autoupdate { get; set; }

        public int intervalupdate { get; set; }

        public string index { get; set; }

        public bool basetag { get; set; }

        public InitPlugins initPlugins { get; set; } = new InitPlugins();
    }
}
