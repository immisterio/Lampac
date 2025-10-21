using Shared.Models.Base;

namespace Shared.Models.Catalog
{
    public class CatalogSettings : BaseSettings, ICloneable
    {
        public CatalogSettings()
        {
            cache_time = 5;
        }

        public bool debug { get; set; }

        public int timeout { get; set; } = 10;

        public List<Microsoft.Playwright.Cookie> cookies { get; set; }

        public bool ignore_no_picture { get; set; } = true;

        public string routeEval { get; set; }

        public string[] serials { get; set; }

        public string[] movies { get; set; }

        public List<MenuSettings> menu { get; set; }

        public ListSettings search { get; set; }

        public ListSettings list { get; set; }

        public ContentParseSettings contentParse { get; set; }

        public CardParseSettings cardParse { get; set; }


        public CatalogSettings Clone()
        {
            return (CatalogSettings)MemberwiseClone();
        }

        object ICloneable.Clone()
        {
            return MemberwiseClone();
        }
    }
}
