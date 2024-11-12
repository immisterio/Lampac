namespace Shared.Model.Online.Lumex
{
    public class EmbedModel
    {
        public string csrf { get; set; }

        public string content_type { get; set; }

        public List<Medium> media { get; set; }
    }
}
