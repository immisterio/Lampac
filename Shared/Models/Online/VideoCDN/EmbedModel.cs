namespace Shared.Models.Online.VideoCDN
{
    public class EmbedModel
    {
        public string type { get; set; } = null!;

        public Dictionary<string, string> voices { get; set; }

        public Dictionary<string, HashSet<int>> voiceSeasons { get; set; }

        public Dictionary<string, List<Season>> serial { get; set; }

        public Dictionary<string, string> movie { get; set; }

        public string quality { get; set; }
    }
}
