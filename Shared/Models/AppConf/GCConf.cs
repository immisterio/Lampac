namespace Shared.Models.AppConf
{
    public class GCConf
    {
        public bool enable { get; set; } = true;

        public bool? Concurrent { get; set; } = false;

        public int? ConserveMemory { get; set; } = 9;

        public int? HighMemoryPercent { get; set; } = 1;

        public bool? RetainVM { get; set; } = false;
    }
}
