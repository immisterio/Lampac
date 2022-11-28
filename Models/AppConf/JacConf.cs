namespace Lampac.Models.AppConf
{
    public class JacConf
    {
        public int timeoutSeconds = 8;

        public string cachetype = "file";

        public int htmlCacheToMinutes = 1;

        public int torrentCacheToMinutes = 2;

        public bool emptycache = false;

        public string apikey = null;

        public bool litejac = true;
    }
}
