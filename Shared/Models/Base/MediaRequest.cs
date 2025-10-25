namespace Shared.Models.Base
{
    public class MediaRequestBase
    {
        public string auth_token { get; set; }

        public string type { get; set; }

        public int? width { get; set; }

        public int? height { get; set; }

        public Dictionary<string, string> headers { get; set; }

        public string proxy { get; set; }

        public string proxy_name { get; set; }

        public bool apnstream { get; set; }

        public bool useproxystream { get; set; } = true;
    }

    public class MediaRequest : MediaRequestBase
    {
        public List<string> urls { get; set; } = new();
    }
}
