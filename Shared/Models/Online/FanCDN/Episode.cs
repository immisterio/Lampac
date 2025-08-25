namespace Shared.Models.Online.FanCDN
{
    public class Episode
    {
        public string title { get; set; }

        public string file { get; set; }

        public string subtitles { get; set; }


        public Dictionary<string, Episode> folder { get; set; }
    }
}
