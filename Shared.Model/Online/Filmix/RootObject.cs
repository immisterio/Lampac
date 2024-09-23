using System.Text.Json.Serialization;

namespace Lampac.Models.LITE.Filmix
{
    public class RootObject
    {
        public PlayerLinks? player_links { get; set; }

        public string? quality { get; set; }
    }

    public class RootObjectTV
    {
        [JsonInclude]
        public Dictionary<string, Dictionary<string, Season>>? SerialVoice { get; set; }
        [JsonInclude]
        public MovieTV[]? Movies { get; set; }
    }
}