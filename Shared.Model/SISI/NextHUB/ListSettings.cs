namespace Shared.Model.SISI.NextHUB
{
    public class ListSettings
    {
        public string uri { get; set; }

        public bool viewsource { get; set; }

        public bool abortMedia { get; set; }

        public bool fullCacheJS { get; set; }


        public ContentParseSettings contentParse { get; set; }
    }
}
