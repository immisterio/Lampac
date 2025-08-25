namespace Shared.Models.Base
{
    public class ProxySettings : ICloneable
    {
        public string name;

        public string pattern;


        public bool useAuth;

        public bool BypassOnLocal;

        public string username;

        public string password;


        public string pattern_auth = "^(?<sheme>[^/]+//)?(?<username>[^:/]+):(?<password>[^@]+)@(?<host>.*)";

        public int maxRequestError = 2;


        public string file;

        public string url;

        public string[] list;


        public string refresh_uri;

        public List<ProxyAction> actions;

        public int actions_attempts = 6;


        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
