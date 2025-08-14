using Shared.Model.Base;

namespace Shared.Model.SISI.NextHUB
{
    public class NxtSettings : BaseSettings
    {
        public NxtSettings()
        {
            cache_time = 5;
        }

        public bool debug { get; set; }

        public bool streamproxy_preview { get; set; }

        public bool ignore_no_picture { get; set; } = true;

        public bool abortMedia { get; set; } = true;

        public bool fullCacheJS { get; set; } = true;

        public bool keepopen { get; set; } = true;

        public List<Microsoft.Playwright.Cookie> cookies { get; set; }

        public RouteSettings route { get; set; }

        public MenuSettings menu { get; set; }

        public ListSettings list { get; set; }

        public ListSettings search { get; set; }

        public ContentParseSettings modelParse { get; set; }

        public ContentParseSettings contentParse { get; set; }

        public ViewSettings view { get; set; }
    }
}
