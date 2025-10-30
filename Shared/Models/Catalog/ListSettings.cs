namespace Shared.Models.Catalog
{
    public class ListSettings
    {
        public string initUrl { get; set; }

        public string initHeader { get; set; }

        public int total_pages { get; set; }

        public int count_page { get; set; }

        public string firstpage { get; set; }

        public string uri { get; set; }

        public string postData { get; set; }

        public ContentParseSettings contentParse { get; set; }
    }
}
