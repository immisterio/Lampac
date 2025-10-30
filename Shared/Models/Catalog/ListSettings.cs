namespace Shared.Models.Catalog
{
    public class ListSettings
    {
        public int total_pages { get; set; }

        public int count_page { get; set; }

        public string firstpage { get; set; }

        public string uri { get; set; }

        public string data { get; set; }

        public ContentParseSettings contentParse { get; set; }
    }
}
