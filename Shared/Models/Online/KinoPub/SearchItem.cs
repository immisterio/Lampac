namespace Shared.Models.Online.KinoPub
{
    public struct SearchItem
    {
        public int id { get; set; }

        public string type { get; set; }

        public string title { get; set; }

        public string voice { get; set; }

        public long? kinopoisk { get; set; }

        public long? imdb { get; set; }

        public int year { get; set; }

        public Dictionary<string, string> posters { get; set; }
    }
}
