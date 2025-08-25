namespace Shared.Models.ServerProxy
{
    public class HlsCacheConf
    {
        public bool enable { get; set; }

        public string plugin { get; set; }

        public List<HlsCachePattern> tasks { get; set; }
    }
}
