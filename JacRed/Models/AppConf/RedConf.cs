namespace JacRed.Models.AppConf
{
    public class RedConf
    {
        public string syncapi = "http://62.112.8.193:9117";

        public int syntime = 60;

        public string[] trackers = new string[] { "rutracker", "rutor", "kinozal", "nnmclub", "megapeer", "bitru", "toloka", "lostfilm", "baibako", "torrentby", "hdrezka", "selezen", "animelayer", "anilibria", "anifilm" };

        public int maxreadfile = 300;

        public bool mergeduplicates = true;

        public bool mergenumduplicates = true;

        public Evercache evercache = new Evercache();
    }
}
