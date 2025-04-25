namespace Shared.Model.SISI.NextHUB
{
    public class ListSettings
    {
        public string uri { get; set; }

        public bool viewsource { get; set; } = true;

        public string patternAbort { get; set; }

        public ContentParseSettings contentParse { get; set; }
    }
}
