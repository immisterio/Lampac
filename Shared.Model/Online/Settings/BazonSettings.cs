using Shared.Model.Base;

namespace Lampac.Models.LITE
{
    public class BazonSettings : BaseSettings
    {
        public BazonSettings(string apihost, string token, bool localip)
        {
            this.apihost = apihost;
            this.token = token;
            this.localip = localip;
        }


        public string token { get; set; }

        public bool localip { get; set; }
    }
}
