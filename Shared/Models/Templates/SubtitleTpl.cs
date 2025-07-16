using System.Text.Json;

namespace Shared.Model.Templates
{
    public struct SubtitleTpl
    {
        List<(string label, string url)> data;

        public SubtitleTpl() : this(10) { }

        public SubtitleTpl(int capacity) { data = new List<(string, string)>(capacity); }

        public bool IsEmpty() => data.Count == 0;

        public void Append(string label, string url)
        {
            if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(url))
                data.Add((label, url));
        }

        public string ToJson() => JsonSerializer.Serialize(ToObject());

        public object ToObject()
        {
            if (data.Count == 0)
                return new List<string>();

            return data.Select(i => new
            {
                method = "link",
                i.url,
                i.label
            });
        }
    }
}
