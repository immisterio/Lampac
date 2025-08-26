namespace Shared.Models.Online.RutubeMovie
{
    public struct Result
    {
        public string id { get; set; }

        public string title { get; set; }

        public long duration { get; set; }

        public Сategory category { get; set; }

        public bool is_hidden { get; set; }
        public bool is_deleted { get; set; }
        public bool is_adult { get; set; }
        public bool is_locked { get; set; }
        public bool is_audio { get; set; }
        public bool is_paid { get; set; }
        public bool is_livestream { get; set; }
    }
}
