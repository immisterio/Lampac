namespace Shared.Model.SISI.NextHUB
{
    public class ViewSettings
    {
        public string addInitScript { get; set; }

        public string playbtn { get; set; }

        public string patternFile { get; set; }

        public bool bindingToIP { get; set; }

        public bool fullCacheJS { get; set; }

        public bool abortMedia { get; set; }

        public string patternAbort { get; set; }

        public bool related { get; set; }

        public int cache_time { get; set; }


        public ContentParseSettings contentParse { get; set; }
    }
}
