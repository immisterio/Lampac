using Newtonsoft.Json.Linq;

namespace Lampac.Models.LITE.Filmix
{
    public class PlayerLinks
    {
        public List<Movie>? movie { get; set; }

        /// <summary>
        /// сезон, (озвучка, (серия, item))
        /// </summary>
        public Dictionary<string, Dictionary<string, JToken>>? playlist { get; set; }
    }
}
