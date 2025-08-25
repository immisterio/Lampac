namespace Shared.Models.Online.VDBmovies
{
    public class EmbedModel
    {
        public List<Episode> movies { get; set; }

        public List<CDNmovies.Voice> serial { get; set; }

        public string quality { get; set; }
    }
}
