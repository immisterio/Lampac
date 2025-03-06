namespace Shared.Models.AppConf
{
    public class PuppeteerConf
    {
        public bool enable { get; set; }

        public bool Headless { get; set; }

        public bool DEV { get; set; }

        public string executablePath { get; set; }

        public string DISPLAY { get; set; }
    }
}
