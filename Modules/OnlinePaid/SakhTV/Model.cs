using System.Collections.Generic;

namespace SakhTV;

public class SearchRoot
{
    public List<SearchItem> movies { get; set; }

    public List<SearchItem> serials { get; set; }
}

public class SearchItem
{
    public string id_alpha { get; set; }

    public string tvshow { get; set; }

    public string name { get; set; }

    public string ru_title { get; set; }

    public string ename { get; set; }

    public string origin_title { get; set; }

    public int year { get; set; }

    public string release_date { get; set; }

    public string poster { get; set; }

    public string cover { get; set; }

    public long kp_id { get; set; }

    public string imdb_url { get; set; }
}

public class MovieDetails
{
    public Source sources { get; set; }
}

public class Source
{
    public string @default { get; set; }
}

public class TvshowDetails
{
    public Season[] seasons { get; set; }
}

public class Season
{
    public int id { get; set; }

    public string index { get; set; }
}

public class EpisodeDetails
{
    public string index { get; set; }

    public string name { get; set; }

    public Rg[] rgs { get; set; }
}

public class Rg
{
    public string rg { get; set; }

    public string runame { get; set; }
}

public class Episode
{
    public string episode_index { get; set; }

    public string episode_playlist { get; set; }
}
