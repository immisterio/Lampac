using Newtonsoft.Json.Linq;

namespace Shared.Models.Online.Filmix
{
    public struct PlayerLinks
    {
        public Movie[] movie { get; set; }

        /// <summary>
        /// сезон, (озвучка, (серия, item))
        /// </summary>
        public Dictionary<string, Dictionary<string, JToken>> playlist { get; set; }
    }
}
