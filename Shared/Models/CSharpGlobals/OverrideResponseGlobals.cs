using Microsoft.AspNetCore.Http;

namespace Shared.Models.CSharpGlobals
{
    public class OverrideResponseGlobals
    {
        public OverrideResponseGlobals() { }

        public OverrideResponseGlobals(string url, HttpRequest rq, RequestModel rinfo)
        {
            request = rq;
            requestInfo = rinfo;
        }

        public string url { get; set; }

        public HttpRequest request { get; set; }

        public RequestModel requestInfo { get; set; }
    }
}
