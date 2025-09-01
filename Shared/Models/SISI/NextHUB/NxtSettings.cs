using Shared.Models.Base;

namespace Shared.Models.SISI.NextHUB
{
    public class NxtSettings : BaseSettings, ICloneable
    {
        public NxtSettings()
        {
            cache_time = 5;
        }

        public bool debug { get; set; }

        public int timeout { get; set; } = 10;

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

        public ListSettings model { get; set; }

        public ContentParseSettings contentParse { get; set; }

        public ViewSettings view { get; set; }


        public NxtSettings Clone()
        {
            return (NxtSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
