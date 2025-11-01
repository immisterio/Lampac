using Shared.Models.Base;

namespace Shared.Models.Catalog
{
    public class CatalogSettings : BaseSettings, ICloneable
    {
        public CatalogSettings()
        {
            cache_time = 5;
        }

        public string args { get; set; }

        public bool hide { get; set; }

        public bool? jsonPath { get; set; }

        public bool search_lazy { get; set; } = true;

        public bool debug { get; set; }

        public int timeout { get; set; } = 10;

        public bool useDefaultHeaders { get; set; } = true;

        public List<Microsoft.Playwright.Cookie> cookies { get; set; }

        public bool ignore_no_picture { get; set; } = true;


        public string[] serial_cats { get; set; }

        public string[] movie_cats { get; set; }

        public string catalog_key { get; set; }

        public List<MenuSettings> menu { get; set; }


        public ListSettings search { get; set; }

        public ListSettings list { get; set; }

        public ContentParseSettings content { get; set; }


        public CardParseSettings card_parse { get; set; }

        public List<SingleNodeSettings> card_args { get; set; }

        public string[] tmdb_injects { get; set; }


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
