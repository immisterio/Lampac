using System.Text.Json.Serialization;

namespace Shared.Model.Online.FilmixTV
{
    public class RootObject
    {
        [JsonInclude]
        public Dictionary<string, Dictionary<string, Season>>? SerialVoice { get; set; }

        [JsonInclude]
        public MovieTV[]? Movies { get; set; }
    }
}
