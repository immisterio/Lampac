namespace JacRed.Models.AppConf
{
    public class JacConf
    {
        public int cacheToMinutes = 5;

        public string search_lang = "query";

        public int timeoutSeconds = 8;


        public TrackerSettings Rutor = new TrackerSettings("https://rutor.info"/*, priority: "torrent"*/);

        public TrackerSettings Megapeer = new TrackerSettings("https://megapeer.vip", enable: false);

        public TrackerSettings TorrentBy = new TrackerSettings("https://torrent.by"/*, priority: "torrent"*/);

        public TrackerSettings Kinozal = new TrackerSettings("https://kinozal.tv");

        public TrackerSettings NNMClub = new TrackerSettings("https://nnmclub.to");

        public TrackerSettings Bitru = new TrackerSettings("https://bitru.org");

        public TrackerSettings Toloka = new TrackerSettings("https://toloka.to");

        public TrackerSettings Rutracker = new TrackerSettings("https://rutracker.org"/*, priority: "torrent"*/);

        public TrackerSettings BigFanGroup = new TrackerSettings("https://bigfangroup.org");

        public TrackerSettings Selezen = new TrackerSettings("https://open.selezen.org"/*, priority: "torrent"*/);

        public TrackerSettings Lostfilm = new TrackerSettings("https://www.lostfilm.tv");

        public TrackerSettings Anilibria = new TrackerSettings("https://www.anilibria.tv");

        public TrackerSettings Animelayer = new TrackerSettings("http://animelayer.ru");

        public TrackerSettings Anifilm = new TrackerSettings("https://anifilm.pro");
    }
}
