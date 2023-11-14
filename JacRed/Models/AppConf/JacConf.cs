using Lampac.Models.JAC;

namespace Lampac.Models.AppConf
{
    public class JacConf
    {
        public bool cache = false;

        public bool emptycache = false;

        public string cachetype = "file";

        public int cacheToMinutes = 5;

        public int torrentCacheToMinutes = 20;

        public string search_lang = "title_original";

        public int timeoutSeconds = 8;


        public TrackerSettings Rutor = new TrackerSettings("http://rutor.info", priority: "torrent");

        public TrackerSettings Megapeer = new TrackerSettings("http://megapeer.vip");

        public TrackerSettings TorrentBy = new TrackerSettings("http://torrent.by", priority: "torrent");

        public TrackerSettings Kinozal = new TrackerSettings("http://kinozal.tv");

        public TrackerSettings NNMClub = new TrackerSettings("https://nnmclub.to");

        public TrackerSettings Bitru = new TrackerSettings("https://bitru.org");

        public TrackerSettings Toloka = new TrackerSettings("https://toloka.to", enable: false);

        public TrackerSettings Rutracker = new TrackerSettings("https://rutracker.net", enable: false, priority: "torrent");

        public TrackerSettings Underverse = new TrackerSettings("https://underver.se", enable: false);

        public TrackerSettings Selezen = new TrackerSettings("https://open.selezen.org", enable: false, priority: "torrent");

        public TrackerSettings Lostfilm = new TrackerSettings("https://www.lostfilm.tv", enable: false);

        public TrackerSettings Anilibria = new TrackerSettings("https://www.anilibria.tv");

        public TrackerSettings Animelayer = new TrackerSettings("http://animelayer.ru", enable: false);

        public TrackerSettings Anifilm = new TrackerSettings("https://anifilm.net");
    }
}
