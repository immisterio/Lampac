using System.Collections.Generic;

namespace BamBoo;

public class EmbedModel
{
    public bool IsEmpty { get; set; }

    public bool isSerial { get; set; }

    public short season { get; set; } = 1;

    public List<Voice> serial { get; set; }

    public List<Video> movie { get; set; }

    public List<Similar> similars { get; set; }
}

public class Similar
{
    public string title { get; set; }

    public string year { get; set; }

    public string href { get; set; }

    public string img { get; set; }
}

public class Voice
{
    public string title { get; set; }

    public List<Series> folder { get; set; }
}

public class Series
{
    public string title { get; set; }

    public string file { get; set; }

    public string type { get; set; }
}

public class Video
{
    public string title { get; set; }

    public string file { get; set; }

    public string type { get; set; }
}
