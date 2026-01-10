using Shared.Models.SISI.Base;
using System.Text.Json.Serialization;

namespace Shared.Models.SISI.OnResult
{
    public struct OnResultPlaylistItem
    {
        public string video { get; set; }

        public string name { get; set; }

        public string picture { get; set; }

        public string preview { get; set; }

        public string quality { get; set; }

        public string time { get; set; }

        public string myarg { get; set; }

        public bool json { get; set; }

        public bool hide { get; set; }

        public bool related { get; set; }

        public OnResultModel? model { get; set; }

        public Dictionary<string, string> qualitys { get; set; }

        public OnResultBookmark? bookmark { get; set; }
    }

    public struct OnResultModel
    {
        [JsonConstructor]
        public OnResultModel(string name, string uri)
        {
            this.uri = uri;
            this.name = name;
        }

        public string uri { get; }

        public string name { get; }
    }

    public struct OnResultBookmark
    {
        [JsonConstructor]
        public OnResultBookmark(string uid, string site, string image, string href)
        {
            this.uid = uid;
            this.site = site;
            this.image = image;
            this.href = href;
        }

        public string uid { get; }

        public string site { get; }

        public string image { get; }

        public string href { get; }
    }


    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    )]
    [JsonSerializable(typeof(OnResultPlaylistItem))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(OnResultModel))]
    [JsonSerializable(typeof(OnResultBookmark))]
    [JsonSerializable(typeof(List<MenuItem>))]
    [JsonSerializable(typeof(MenuItem))]
    public partial class SisiResultJsonContext : JsonSerializerContext
    {

    }
}
