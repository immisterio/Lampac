namespace Shared.Models.Online.AniLibria
{
    public struct Poster
    {
        public Poster_url small { get; set; }

        public Poster_url medium { get; set; }

        public Poster_url original { get; set; }
    }

    public struct Poster_url
    {
        public string url { get; set; }
    }
}
