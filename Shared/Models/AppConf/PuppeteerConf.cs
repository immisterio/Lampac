namespace Shared.Models.AppConf
{
    public class PuppeteerConf
    {
        public bool enable { get; set; }

        public bool keepopen { get; set; }

        public bool Headless { get; set; } = true;

        public string executablePath { get; set; }
    }
}
