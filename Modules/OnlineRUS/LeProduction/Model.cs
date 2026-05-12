using Shared.Models.Templates;
using System.Collections.Generic;

namespace LeProduction;

public class EmbedModel
{
    public List<StreamQualityDto> movie { get; set; }

    public string season { get; set; }

    public SerialModel[] serial { get; set; }
}

public class SerialModel
{
    public string comment { get; set; }

    public string file { get; set; }

    public List<StreamQualityDto> streams { get; set; }
}
