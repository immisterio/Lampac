using Newtonsoft.Json.Linq;

namespace Shared.Models.Online.Filmix
{
    public class PlayerLinks
    {
        public List<Movie> movie { get; set; }

        /// <summary>
        /// сезон, (озвучка, (серия, item))
        /// </summary>
        public Dictionary<string, Dictionary<string, JToken>> playlist { get; set; }
    }
}
