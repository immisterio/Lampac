namespace Shared.Models.AppConf
{
    public class WafConf
    {
        public bool enable { get; set; }

        public List<string> whiteIps { get; set; }

        public int limit_req { get; set; }

        /// <summary>
        /// uri_pattern: limit_req
        /// </summary>
        public Dictionary<string, int> limit_map { get; set; }

        public List<string> ipsDeny { get; set; }

        public List<string> ipsAllow { get; set; }

        public List<string> countryDeny { get; set; }

        public List<string> countryAllow { get; set; }

        /// <summary>
        /// header_key: regex
        /// </summary>
        public Dictionary<string, string> headersDeny { get; set; }
    }
}
