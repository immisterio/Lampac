namespace JacRed.Models.AppConf
{
    public class Evercache
    {
        public bool enable = false;

        public int validHour = 1;

        public int maxOpenWriteTask { get; set; } = 1000;

        public int dropCacheTake { get; set; } = 100;
    }
}
