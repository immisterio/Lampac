namespace Shared.Models.SISI.NextHUB
{
    public class ViewSettings
    {
        public string initUrlEval { get; set; }

        public string addInitScript { get; set; }

        public string routeEval { get; set; }

        public string eval { get; set; }

        public string evalJS { get; set; }

        public string playbtn { get; set; }

        public string waitForSelector { get; set; }

        public float waitForSelector_timeout { get; set; } = 5000;

        public string patternFile { get; set; }

        public bool waitLocationFile { get; set; }

        public bool waitForResponse { get; set; }

        public RegexMatchSettings iframe { get; set; }

        public SingleNodeSettings nodeFile { get; set; }

        public RegexMatchSettings regexMatch { get; set; }

        public bool bindingToIP { get; set; }

        public bool fullCacheJS { get; set; } = true;

        public bool abortMedia { get; set; } = true;

        public string patternAbort { get; set; }

        public string patternAbortEnd { get; set; }

        public string patternWhiteRequest { get; set; }

        public bool related { get; set; }

        public ContentParseSettings relatedParse { get; set; }

        public bool NetworkIdle { get; set; }

        public int cache_time { get; set; } = 15;


        public bool viewsource { get; set; }

        public string priorityBrowser { get; set; }

        public bool keepopen { get; set; } = true;
    }
}
