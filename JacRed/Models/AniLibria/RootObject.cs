namespace JacRed.Models.AniLibria
{
    public class RootObject
    {
        public Names names { get; set; }

        public string code { get; set; }

        public Torrents torrents { get; set; }

        public Season season { get; set; }

        public long updated { get; set; }

        public long last_change { get; set; }
    }
}
