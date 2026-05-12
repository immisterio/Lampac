using System.Collections.Generic;

namespace Ashdi;

public class EmbedModel
{
    public bool IsEmpty { get; set; }

    public Movie movie { get; set; }

    public Voice[] serial { get; set; }
}

public class Movie
{
    public string hls { get; set; }

    public List<Cc> subs { get; set; }
}

public class Season
{
    public string title { get; set; }

    public Series[] folder { get; set; }
}

public class Series
{
    public string title { get; set; }

    public string file { get; set; }

    public string subtitle { get; set; }
}

public class Voice
{
    public string title { get; set; }

    public Season[] folder { get; set; }
}

public class Cc
{
    public string url { get; set; }

    public string name { get; set; }
}
