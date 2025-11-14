namespace Shared.Models.SISI.Base
{
    public class SisiConf
    {
        public bool xdb { get; set; }

        public bool lgbt { get; set; }

        public bool NextHUB { get; set; }

        public string[] NextHUB_sites_enabled { get; set; }

        public bool rsize { get; set; }

        public string rsize_host { get; set; }

        public string bypass_host { get; set; }

        public string[] rsize_disable { get; set; }

        public string[] proxyimg_disable { get; set; }

        public int heightPicture { get; set; }

        public int widthPicture { get; set; }


        public bool spider { get; set; }

        public string component { get; set; }

        public string iconame { get; set; }


        public bool push_all { get; set; }

        public bool forced_checkRchtype { get; set; }


        public BookmarksConf bookmarks { get; set; }

        public HistoryConf history { get; set; }


        public Dictionary<string, string> appReplace { get; set; }
    }
}
