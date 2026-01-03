using System.Text.Json;

namespace Shared.Models.Templates
{
    public struct SubtitleTpl
    {
        public List<SubtitleDto> data { get; private set; }

        public SubtitleTpl() : this(10) { }

        public SubtitleTpl(int capacity) 
        { 
            data = new List<SubtitleDto>(capacity); 
        }

        public bool IsEmpty => data == null || data.Count == 0;

        public void Append(string label, string url)
        {
            if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(url))
                data.Add(new SubtitleDto(url, label));
        }

        public string ToJson() => JsonSerializer.Serialize(ToObject());

        public IReadOnlyList<SubtitleDto> ToObject(bool emptyToNull = false)
        {
            if (IsEmpty)
                return emptyToNull ? null : Array.Empty<SubtitleDto>();

            return data;
        }
    }


    public readonly struct SubtitleDto
    {
        public string method { get; }
        public string url { get; }
        public string label { get; }

        public SubtitleDto(string url, string label)
        {
            method = "link";
            this.url = url;
            this.label = label;
        }
    }
}
