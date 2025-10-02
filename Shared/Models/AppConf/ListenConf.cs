using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace Shared.Models.AppConf
{
    public class ListenConf
    {
        public string ip { get; set; }

        public int port { get; set; }

        public bool compression { get; set; }

        public string sock { get; set; }

        public string scheme { get; set; }

        public string host { get; set; }

        public string frontend { get; set; }

        public string localhost { get; set; }

        public int? keepalive { get; set; }

        public HttpProtocols? endpointDefaultsProtocols { get; set; }
    }
}
