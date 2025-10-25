namespace Shared.Models.Base
{
    public class CorseuRequest
    {
        public string browser { get; set; }

        public string url { get; set; }

        public string method { get; set; }

        public string data { get; set; }

        public int? httpversion { get; set; }

        public int? timeout { get; set; }

        public string encoding { get; set; }

        public Dictionary<string, string> headers { get; set; }

        public bool? defaultHeaders { get; set; }

        public bool? autoredirect { get; set; }

        public string proxy { get; set; }

        public string proxy_name { get; set; }

        public bool? headersOnly { get; set; }

        public string auth_token { get; set; }
    }
}
