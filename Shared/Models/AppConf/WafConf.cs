namespace Shared.Models.AppConf
{
    public class WafConf
    {
        public bool enable { get; set; }

        public string[] whiteIps { get; set; }

        public int limit_req { get; set; }

        public string[] ipsDeny { get; set; }

        public string[] ipsAllow { get; set; }

        public string[] countryDeny { get; set; }

        public string[] countryAllow { get; set; }

        /// <summary>
        /// header_key: regex
        /// </summary>
        public Dictionary<string, string> headersDeny { get; set; }
    }
}
