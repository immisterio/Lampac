namespace Shared.Models.AppConf
{
    public class OnlineConf
    {
        public string findkp { get; set; }

        public bool checkOnlineSearch { get; set; }

        public bool spider { get; set; }

        public string spiderName { get; set; }


        public string component { get; set; }

        public string name { get; set; }

        public string description { get; set; }

        public bool version { get; set; }

        public bool btn_priority_forced { get; set; }

        public bool showquality { get; set; }


        public string apn { get; set; }

        public Dictionary<string, string> appReplace { get; set; }


        public List<string> with_search { get; set; }
    }
}
