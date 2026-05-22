namespace FilmixTVModels;

public class RootObject
{
    public Dictionary<string, Dictionary<string, Season>> SerialVoice { get; set; }

    public MovieTV[] Movies { get; set; }

    public List<Filmix.Models.SearchModel> items { get; set; }
}

public class MovieTV
{
    public File[] files { get; set; }

    public string voiceover { get; set; }
}

public class Season
{
    public short season { get; set; }

    public Dictionary<string, Episode> episodes { get; set; }
}

public class Episode
{
    public short episode { get; set; }

    public File[] files { get; set; }
}

public class File
{
    public string url { get; set; }

    public int quality { get; set; }
}
