namespace Shared.Models.Online.Lumex
{
    public class EmbedModel
    {
        public string csrf { get; set; }

        public string tag_url { get; set; }

        public string content_type { get; set; }

        public Medium[] media { get; set; }
    }
}
