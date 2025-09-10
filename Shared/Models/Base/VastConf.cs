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

        /// <summary>
        /// ru,ua,kz,etc
        /// </summary>
        public string region { get; set; }

        /// <summary>
        /// 'android','noname','webos','tizen','apple','browser','nw','philips','orsay','apple_tv','netcast','electron'
        /// </summary>
        public string platform { get; set; }

        /// <summary>
        /// tv, mobile
        /// </summary>
        public string screen { get; set; }
    }
}
