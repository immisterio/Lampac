namespace Shared.Models.AppConf
{
    public class GCConf
    {
        public bool enable { get; set; }

        public bool aggressive { get; set; }

        public bool? Concurrent { get; set; }

        public int? ConserveMemory { get; set; }

        public int? HighMemoryPercent { get; set; }

        public bool? RetainVM { get; set; }
    }
}
