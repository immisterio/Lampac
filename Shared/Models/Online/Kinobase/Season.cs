namespace Lampac.Models.LITE.Kinobase
{
    public class Season
    {
        public long id { get; set; }

        public string file { get; set; }

        public string title { get; set; }

        public string comment { get; set; }

        public string subtitle { get; set; }

        public List<Season> folder { get; set; }
    }
}
