namespace Shared.Models.Online.Rezka
{
    public class SearchModel
    {
        public bool IsEmpty { get; set; }

        public string content { get; set; }

        public string href { get; set; }

        public string search_uri { get; set; }

        public List<SimilarModel> similar { get; set; }
    }
}
