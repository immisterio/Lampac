using Newtonsoft.Json.Linq;

namespace Shared.Models.Catalog
{
    public class PlaylistItem
    {
        public string id { get; set; }

        public bool is_serial { get; set; }

        public string title { get; set; }

        public string original_title { get; set; }

        public string img { get; set; }

        public string year { get; set; }

        public string card { get; set; }

        public JObject args { get; set; }
    }
}
