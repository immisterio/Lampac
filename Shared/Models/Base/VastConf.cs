namespace Shared.Models.Base
{
    public class VastConf
    {
        public VastConf() { }

        public VastConf(string url, string msg) 
        {
            this.url = url;
            this.msg = msg;
        }

        public string url { get; set; }

        public string msg { get; set; }
    }
}
