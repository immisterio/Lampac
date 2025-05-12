namespace Shared.Model.Online.VeoVeo
{
    public class Movie
    {
        public long id { get; set; }

        public long? kinopoiskId { get; set; }

        public string? imdbId { get; set; }

        public string? originalTitle { get; set; }

        public string? title { get; set; }
    }
}
