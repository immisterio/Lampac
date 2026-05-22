using System.Collections.Generic;

namespace HDVB;

public class Video
{
    public string type { get; set; }

    public string iframe_url { get; set; }

    public string translator { get; set; }

    public int? season_number { get; set; }

    public List<Season> serial_episodes { get; set; }

    public long? kinopoisk_id { get; set; }

    public string title_ru { get; set; }

    public string title_en { get; set; }

    public int? year { get; set; }

    public string poster { get; set; }
}

public class Season
{
    public int? season_number { get; set; }

    public List<short> episodes { get; set; }
}

public class Folder
{
    public string id { get; set; }

    public string episode { get; set; }

    public Folder[] folder { get; set; }

    public string title { get; set; }

    public string file { get; set; }
}
