namespace Shared.Models.Catalog
{
    public class ListSettings
    {
        public int total_pages { get; set; } = 0;

        public string firstpage { get; set; }

        public string uri { get; set; }

        public ContentParseSettings contentParse { get; set; }
    }
}
