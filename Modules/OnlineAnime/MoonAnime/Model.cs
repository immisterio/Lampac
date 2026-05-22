using System.Collections.Generic;

namespace MoonAnime;

public class SearchRoot
{
    public List<SearchItem> anime_list { get; set; }
}

public class SearchItem
{
    public long id { get; set; }

    public string title { get; set; }

    public int year { get; set; }

    public string poster { get; set; }
}

public class Episode
{
    public short episode { get; set; }

    public string vod { get; set; }
}
