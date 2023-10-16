namespace Shared.Model.Online.KinoPub
{
    public class SearchItem
    {
        public int id { get; set; }

        public string title { get; set; }

        public string voice { get; set; }

        public long? kinopoisk { get; set; }

        public long? imdb { get; set; }

        public int year { get; set; }
    }
}
