using System.Text.Json;

namespace Shared.Model.Templates
{
    public class SubtitleTpl
    {
        List<(string label, string url)> data;

        public SubtitleTpl(int capacity = 10) { data = new List<(string, string)>(capacity); }

        public bool IsEmpty() => data.Count == 0;

        public void Append(string? label, string? url)
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
