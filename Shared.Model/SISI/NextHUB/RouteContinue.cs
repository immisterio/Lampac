namespace Shared.Model.SISI.NextHUB
{
    public class RouteContinue
    {
        public RouteContinue(string url, byte[]? postData = null, Dictionary<string, string>? headers = null)
        {
            this.url = url;
            this.postData = postData;
            this.headers = headers;
        }

        public string url { get; set; }

        public byte[]? postData { get; set; }

        public Dictionary<string, string>? headers { get; set; }
    }
}
