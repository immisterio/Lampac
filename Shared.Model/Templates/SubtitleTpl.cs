using System.Text;
using System.Text.RegularExpressions;

namespace Shared.Model.Templates
{
    public class SubtitleTpl
    {
        List<(string label, string url)> data = new List<(string, string)>();

        public SubtitleTpl() { }

        public SubtitleTpl(int capacity) { data.Capacity = capacity; }

        public bool IsEmpty() => data.Count == 0;

        public void Append(string? label, string? url)
        {
            if (!string.IsNullOrEmpty(label) && !string.IsNullOrEmpty(url))
                data.Add((label, url));
        }

        public string ToHtml()
        {
            if (data.Count == 0)
                return string.Empty;

            var build = new StringBuilder();

            foreach (var i in data)
                build.Append("{\"label\": \"" + i.label?.Replace("\"", "%22")?.Replace("'", "%27") + "\",\"url\": \"" + i.url + "\"},");

            return Regex.Replace(build.ToString(), ",$", "");
        }
    }
}
