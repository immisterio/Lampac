using Shared.Models.Templates;
using System.Collections.Generic;

namespace VideoDB;

public class EmbedModel
{
    public RootObject[] pl { get; set; }

    public bool movie { get; set; }

    public bool obfuscation { get; set; }

    public string quality { get; set; }
}

public class RootNode
{
    public RootObject[] file { get; set; }
}

public class RootObject
{
    public string title { get; set; }

    public string file { get; set; }

    public List<StreamQualityDto> streams { get; set; }

    public Folder[] folder { get; set; }
}

public class Folder
{
    public string title { get; set; }

    public Folder[] folder { get; set; }

    public string file { get; set; }

    public List<StreamQualityDto> streams { get; set; }
}
