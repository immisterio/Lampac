namespace Shared.Models.Online.Lumex
{
    public struct DatumDB
    {
        public long id { get; set; }

        public long kinopoisk_id { get; set; }

        public string imdb_id { get; set; }

        public string ru_title { get; set; }

        public string orig_title { get; set; }

        public string content_type { get; set; }

        public string year { get; set; }
    }
}
