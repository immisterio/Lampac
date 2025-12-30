namespace Shared.Models.AppConf
{
    public class RchConf
    {
        public bool enable { get; set; }

        public bool requiredConnected { get; set; }

        public string notSupportMsg { get; set; }

        public string[] blacklistHost { get; set; }
    }
}
