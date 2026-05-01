using Newtonsoft.Json;
using System.Collections.Generic;

namespace ForkXML;

public class ForkPlaylistItem
{
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string search_on { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string title { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string playlist_url { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string stream_url { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string description { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string logo_30x30 { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string ident { get; set; }


    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string position { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string template { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string before { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string after { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public int br { get; set; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<ForkPlaylistItem> submenu { get; set; }
}
