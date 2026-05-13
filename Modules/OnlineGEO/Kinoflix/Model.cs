using System.Collections.Generic;
using Shared.Models.Templates;

namespace Kinoflix;

public class EmbedModel
{
    public string referer { get; set; }

    public List<PlayerModel> items { get; set; }
}

public class SearchModel
{
    public string link { get; set; }

    public SimilarTpl similar { get; set; }
}

public class PlayerModel
{
    public string file { get; set; }

    public string title { get; set; }

    public List<Folder> folder { get; set; }

    public List<Subtitle> subtitles { get; set; }
}

public class Folder
{
    public string title { get; set; }

    public string file { get; set; }

    public List<Subtitle> subtitles { get; set; }
}

public class Subtitle
{
    public string label { get; set; }

    public string file { get; set; }
}

