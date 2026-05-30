using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates;

public class SubtitleTpl
{
    private readonly int _capacity;

    public List<SubtitleDto> data { get; set; }

    public SubtitleTpl(int capacity = 10)
    {
        _capacity = capacity;
    }

    public void Append(string label, string url)
    {
        if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(url))
        {
            data ??= new List<SubtitleDto>(_capacity);
            data.Add(new SubtitleDto(url, label));
        }
    }

    public bool IsEmpty
        => data == null || data.Count == 0;

    public string ToJson()
    {
        if (IsEmpty)
            return "[]";

        return JsonSerializer.Serialize(data, SubtitleJsonContext.Default.ListSubtitleDto);
    }

    public IReadOnlyList<SubtitleDto> ToObject(bool emptyToNull = false)
    {
        if (IsEmpty)
            return emptyToNull ? null : Array.Empty<SubtitleDto>();

        return data;
    }
}


[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
)]
[JsonSerializable(typeof(SubtitleDto))]
[JsonSerializable(typeof(List<SubtitleDto>))]
[JsonSerializable(typeof(IReadOnlyList<SubtitleDto>))]
public partial class SubtitleJsonContext : JsonSerializerContext
{
}

public class SubtitleDto
{
    public string method => "link";
    public string url { get; }
    public string label { get; }

    [JsonConstructor]
    [Newtonsoft.Json.JsonConstructor]
    public SubtitleDto(string url, string label)
    {
        this.url = url;
        this.label = label;
    }
}
