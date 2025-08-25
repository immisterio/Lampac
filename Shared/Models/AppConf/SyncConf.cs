namespace Shared.Models.AppConf
{
    public class SyncConf
    {
        public bool enable { get; set; }

        /// <summary>
        /// master
        /// slave
        /// </summary>
        public string type { get; set; }

        public string initconf { get; set; }

        public bool sync_full { get; set; } = true;

        public string api_host { get; set; }

        public string api_passwd { get; set; }

        public Dictionary<string, string> override_conf { get; set; }
    }
}
