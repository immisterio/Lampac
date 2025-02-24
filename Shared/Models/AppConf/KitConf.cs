namespace Shared.Models.AppConf
{
    public class KitConf
    {
        public bool enable { get; set; }

        public string path { get; set; }

        public int cacheToSeconds { get; set; }

        public bool rhub_fallback { get; set; }
    }
}
