namespace Shared.Model.Online.Rezka
{
    public class EmbedModel
    {
        public bool IsEmpty { get; set; }

        public string? content { get; set; }

        public string? id { get; set; }

        public List<SimilarModel>? similar { get; set; }
    }
}
