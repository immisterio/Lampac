namespace Shared.Models.AppConf
{
    public class StorageConf
    {
        public bool enable { get; set; }

        public long max_size { get; set; }

        public bool brotli { get; set; }

        public bool md5name { get; set; }
    }
}
