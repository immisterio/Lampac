namespace Shared.Models.Online.VDBmovies
{
    public struct MovieDB
    {
        public string id { get; set; }
        public string ru_title { get; set; }
        public string orig_title { get; set; }
        public string imdb_id { get; set; }
        public long? kinopoisk_id { get; set; }
        public int year { get; set; }
    }
}
