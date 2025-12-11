namespace Shared.Models.AppConf
{
    public class FileCacheConf
    {
        /// <summary>
        /// 1 GB
        /// </summary>
        public long freeDiskSpace { get; set; } = 1073741824;

        public int html { get; set; }

        public int torrent { get; set; }

        public int hls { get; set; }
    }
}
