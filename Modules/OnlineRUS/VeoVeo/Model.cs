using System.Collections.Generic;

namespace VeoVeo;

public class Movie
{
    public long id { get; set; }

    public short year { get; set; }

    public long? kinopoiskId { get; set; }

    public string imdbId { get; set; }

    public string originalTitle { get; set; }

    public string title { get; set; }
}

public class CatalogItem
{
    public short order { get; set; }

    public string title { get; set; }

    public Season season { get; set; }

    public List<EpisodeVariant> episodeVariants { get; set; }
}

public class Season
{
    public int order { get; set; }
}

public class EpisodeVariant
{
    public string title { get; set; }

    public string filepath { get; set; }
}

public class ParsedResponse
{
    public List<ParsedSource> sources { get; set; }
}

public class ParsedSource
{
    public string link { get; set; }
}
