using Shared.Models.Templates;

namespace AsiaGe;

public class EmbedModel
{
    public bool IsEmpty { get; set; }

    public SimilarTpl similar { get; set; }
}

public class SerialModel
{
    public string title { get; set; }

    public string download { get; set; }

    public string file { get; set; }
}
