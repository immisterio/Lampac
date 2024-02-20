namespace Shared.Model.Templates
{
    public class StreamQualityTpl
    {
        List<(string link, string quality)> data = new List<(string, string)>();

        public StreamQualityTpl() { }

        public StreamQualityTpl(IEnumerable<(string link, string quality)>? streams) { if (streams != null) data = streams.ToList(); }

        public bool IsEmpty() => !data.Any();

        public void Append(string? link, string? quality)
        {
            if (!string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(quality))
                data.Add((link, quality));
        }

        public string ToHtml()
        {
            if (data.Count == 0)
                return string.Empty;

            return string.Join(",", data.Select(s => $"\"{s.quality}\":\"{s.link}\""));
        }
    }
}
