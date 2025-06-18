namespace Shared.Model.Online.VDBmovies
{
    public class EmbedModel
    {
        public List<Episode> movies { get; set; }

        public List<Lampac.Models.LITE.CDNmovies.Voice> serial { get; set; }

        public string quality { get; set; }
    }
}
