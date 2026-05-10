using System.Collections.Generic;

namespace Collaps;

public class EmbedModel
{
    public Movie movie { get; set; }

    public SerialModel[] serial { get; set; }
}

public class Movie
{
    public string name { get; set; }

    public string voicename { get; set; }

    public string hls { get; set; }

    public string dash { get; set; }

    public Cc[] cc { get; set; }
}

public class SerialModel
{

    public int season { get; set; }

    public Episode[] episodes { get; set; }
}

public class Episode
{
    public string episode { get; set; }

    public string hls { get; set; }

    public string dasha { get; set; }

    public string dash { get; set; }

    public Cc[] cc { get; set; }

    public Audio audio { get; set; }
}

public class Audio
{
    public string[] names { get; set; }
}

public class Cc
{
    public string url { get; set; }

    public string name { get; set; }
}

public class RootSearch
{
    public List<ResultSearch> results { get; set; }
}

public class ResultSearch
{
    public int id { get; set; }

    public string name { get; set; }

    public string origin_name { get; set; }

    public int year { get; set; }

    public string poster { get; set; }
}
