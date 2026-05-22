using System.Collections.Generic;

namespace UAFilm;

public class SearchRoot
{
    public List<Result> results;
}

public class Result
{
    public int id { get; set; }

    public string imdb_id { get; set; }

    public long tmdb_id { get; set; }

    public string name { get; set; }

    public string original_title { get; set; }

    public int year { get; set; }

    public string poster { get; set; }
}

public class ItemRoot
{
    public TitleModel title;

    public Episode episodes { get; set; }
}

public class WatchRoot
{
    public Video video { get; set; }
}

public class TitleModel
{
    public bool is_series { get; set; }

    public short seasons_count { get; set; }

    public List<Video> videos { get; set; }
}

public class Video
{
    public string name { get; set; }

    public string origin { get; set; }

    public string src { get; set; }
}

public class Episode
{
    public List<EpisodeData> data { get; set; }
}

public class EpisodeData
{
    public short season_number { get; set; }

    public short episode_number { get; set; }

    public PrimaryVideo primary_video { get; set; }
}

public class PrimaryVideo
{
    public int id { get; set; }

    public string name { get; set; }
}
