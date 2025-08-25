namespace Shared.Models.SISI.NextHUB
{
    public class ListSettings
    {
        public int total_pages { get; set; } = 0;

        public string firstpage { get; set; }

        public string uri { get; set; }

        public string format { get; set; }

        public bool viewsource { get; set; } = true;

        public string waitForSelector { get; set; }

        public float waitForSelector_timeout { get; set; } = 5000;

        public string patternAbort { get; set; }

        public string routeEval { get; set; }

        public ContentParseSettings contentParse { get; set; }
    }
}
