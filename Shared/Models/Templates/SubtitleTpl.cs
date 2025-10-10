using System.Text.Json;

namespace Shared.Models.Templates
{
    public struct SubtitleTpl
    {
        public List<(string label, string url)> data { get; set; }

        public SubtitleTpl() : this(10) { }

        public SubtitleTpl(int capacity) { data = new List<(string, string)>(capacity); }

        public bool IsEmpty() => data.Count == 0;

        public void Append(string label, string url)
        {
            if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(url))
                data.Add((label, url));
        }

        public string ToJson() => JsonSerializer.Serialize(ToObject());

        public object ToObject(bool emptyToNull = false)
        {
            if (data.Count == 0)
                return emptyToNull ? null : new List<string>();

            return data.Select(i => new
            {
                method = "link",
                i.url,
                i.label
            });
        }
    }
}
