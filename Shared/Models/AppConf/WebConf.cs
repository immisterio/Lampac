namespace Shared.Models.AppConf
{
    public class WebConf
    {
        public bool autoupdate { get; set; }

        public string tree { get; set; }

        public int intervalupdate { get; set; }

        public string index { get; set; }

        public string path { get; set; }

        public bool basetag { get; set; }

        public InitPlugins initPlugins { get; set; } = new InitPlugins();


        public Dictionary<string, string> appReplace { get; set; } = new Dictionary<string, string>();

        public string eval { get; set; }


        public Dictionary<string, string> cssReplace { get; set; } = new Dictionary<string, string>();

        public string cssEval { get; set; }
    }
}
