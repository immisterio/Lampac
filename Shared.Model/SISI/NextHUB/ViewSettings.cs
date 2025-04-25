namespace Shared.Model.SISI.NextHUB
{
    public class ViewSettings
    {
        public string? addInitScript { get; set; }

        public string? evaluate { get; set; }

        public string? playbtn { get; set; }

        public int playbtn_timeout { get; set; } = 4;

        public string patternFile { get; set; }

        public bool bindingToIP { get; set; }

        public bool fullCacheJS { get; set; } = true;

        public bool abortMedia { get; set; } = true;

        public string? patternAbort { get; set; }

        public bool related { get; set; }

        public bool NetworkIdle { get; set; }

        public int cache_time { get; set; } = 15;


        public string? priorityBrowser { get; set; }

        public bool keepopen { get; set; } = true;


        public ContentParseSettings? contentParse { get; set; }
    }
}
