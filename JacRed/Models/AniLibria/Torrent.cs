namespace JacRed.Models.AniLibria
{
    public class Torrent
    {
        public Series series { get; set; }

        public Quality quality { get; set; }

        public int leechers { get; set; }

        public int seeders { get; set; }

        public string url { get; set; }

        public long total_size { get; set; }
    }
}
