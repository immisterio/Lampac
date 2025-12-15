namespace Shared.Models.AppConf
{
    public class CorseuConf
    {
        public string[] tokens { get; set; }

        public CorseuRules[] rules { get; set; }
    }

    public class CorseuRules
    {
        public string method { get; set; }

        public string url { get; set; }

        public bool replace { get; set; }

        public Dictionary<string, string> headers { get; set; }
    }
}