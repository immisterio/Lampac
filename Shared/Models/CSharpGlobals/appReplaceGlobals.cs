namespace Shared.Models.CSharpGlobals
{
    public class appReplaceGlobals
    {
        public appReplaceGlobals() { }

        public appReplaceGlobals(string file, string host, string token, RequestModel requestInfo, string type = null)
        {
            this.file = file;
            this.host = host;
            this.token = token;
            this.type = type;
            this.requestInfo = requestInfo;
        }

        public string file;
        public string host;
        public string token;
        public string type;
        public RequestModel requestInfo;
    }
}
