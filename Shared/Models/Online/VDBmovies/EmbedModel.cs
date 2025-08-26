namespace Shared.Models.Online.VDBmovies
{
    public class EmbedModel
    {
        public Episode[] movies { get; set; }

        public CDNmovies.Voice[] serial { get; set; }

        public string quality { get; set; }
    }
}
