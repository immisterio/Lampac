using System.Collections.Concurrent;

namespace Shared.Model.Base
{
    public class ProxySettings : ICloneable
    {
        public string? name;

        public string? pattern;


        public bool useAuth;

        public bool BypassOnLocal;

        public string? username;

        public string? password;


        public string? file;

        public string? url;

        public ConcurrentBag<string>? list;


        public string? refresh_uri;

        public List<ProxyAction>? actions;


        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
