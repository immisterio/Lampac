namespace Shared.Models.Online.Kinobase
{
    public struct Season
    {
        public long id { get; set; }

        public string file { get; set; }

        public string title { get; set; }

        public string comment { get; set; }

        public string subtitle { get; set; }

        public Season[] folder { get; set; }
    }
}
