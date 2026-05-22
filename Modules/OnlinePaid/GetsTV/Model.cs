using System;
using System.Collections.Generic;

namespace GetsTV;

public class Title
{
    public string ru { get; set; }

    public string en { get; set; }
}

public class MediaItem
{
    public string _id { get; set; }

    public string trName { get; set; }

    public string sourceType { get; set; }
}

public class Translation
{
    public string _id { get; set; }

    public int trId { get; set; }

    public string trName { get; set; }
}

public class Episode
{
    public short episodeNum { get; set; }

    public List<Translation> trs { get; set; }
}

public class Season
{
    public short seasonNum { get; set; }

    public List<Episode> episodes { get; set; }
}

public class MovieDetailsRoot
{
    public string type { get; set; }

    public List<MediaItem> media { get; set; }

    public List<Season> seasons { get; set; }
}

public class Subtitle
{
    public string lang { get; set; }

    public string url { get; set; }
}

public class Resolution
{
    public int type { get; set; }

    public string url { get; set; }
}

public class Movie
{
    public Title title { get; set; }
}

public class Media
{
    public Movie movie { get; set; }
}

public class MediaStreamRoot
{
    public List<Subtitle> subtitles { get; set; }

    public List<Resolution> resolutions { get; set; }

    public Media media { get; set; }
}

public class SearchItem
{
    public string _id { get; set; }

    public Title title { get; set; }

    public DateTime released { get; set; }

    public string poster { get; set; }

    public string contentType { get; set; }
}
