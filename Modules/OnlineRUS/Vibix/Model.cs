using Shared.Models.Templates;
using System.Collections.Generic;

namespace Vibix;

public class Item
{
    public string title { get; set; }

    public Item[] folder { get; set; }

    public string file { get; set; }


    public List<StreamQualityDto> streams { get; set; }

    public Dictionary<string, List<StreamQualityDto>> voices { get; set; }
}
