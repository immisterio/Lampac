namespace Shared.Models.Online.AniLibria
{
    public struct RootObject
    {
        public Names names { get; set; }

        public string code { get; set; }

        public Season season { get; set; }

        public Player player { get; set; }

        public Poster posters { get; set; }
    }
}
