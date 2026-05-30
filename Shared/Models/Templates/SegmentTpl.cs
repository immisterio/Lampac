using System.Text.Json.Serialization;

namespace Shared.Models.Templates;

public class SegmentTpl
{
    public List<SegmentDto> ads { get; set; }

    public List<SegmentDto> skips { get; set; }

    public SegmentTpl()
    {
        ads = new List<SegmentDto>(5);
        skips = new List<SegmentDto>(5);
    }

    public bool IsEmpty
        => (ads == null || ads.Count == 0) && (skips == null || skips.Count == 0);

    public void ad(int start, int end)
    {
        if (start >= 0 && end >= 0 && end >= start)
            ads.Add(new SegmentDto(start == 0 ? 1 : start, end));
    }

    public void skip(int start, int end)
    {
        if (start >= 0 && end >= 0 && end >= start)
            skips.Add(new SegmentDto(start == 0 ? 1 : start, end));
    }

    public Dictionary<string, IReadOnlyList<SegmentDto>> ToObject()
    {
        if (IsEmpty)
            return null;

        return new Dictionary<string, IReadOnlyList<SegmentDto>>()
        {
            ["ad"] = ads,
            ["skip"] = skips
        };
    }
}

public class SegmentDto
{
    public int start { get; }
    public int end { get; }

    [JsonConstructor]
    [Newtonsoft.Json.JsonConstructor]
    public SegmentDto(int start, int end)
    {
        this.start = start;
        this.end = end;
    }
}
