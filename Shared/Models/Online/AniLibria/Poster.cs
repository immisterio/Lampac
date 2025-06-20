namespace Shared.Model.Online.AniLibria
{
    public class Poster
    {
        public Poster_url small { get; set; }

        public Poster_url medium { get; set; }

        public Poster_url original { get; set; }
    }

    public class Poster_url
    {
        public string url { get; set; }
    }
}
