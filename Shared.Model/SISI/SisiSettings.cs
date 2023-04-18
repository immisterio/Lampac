namespace Lampac.Models.SISI
{
    public class SisiSettings
    {
        public SisiSettings(string host, bool enable = true, bool useproxy = false)
        {
            this.host = host;
            this.enable = enable;
            this.useproxy = useproxy;
        }


        public string host { get; set; }

        public string? webcorshost { get; set; }

        public bool enable { get; set; }

        public bool useproxy { get; set; }

        public bool streamproxy { get; set; }

        public string corsHost()
        {
            if (string.IsNullOrWhiteSpace(webcorshost))
                return host;

            return $"{webcorshost}/{host}";
        }

        public string corsHost(string uri)
        {
            if (string.IsNullOrWhiteSpace(webcorshost) || string.IsNullOrWhiteSpace(uri) || uri.Contains(webcorshost))
                return uri;

            return $"{webcorshost}/{uri}";
        }
    }
}
