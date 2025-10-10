using Shared.Models.Events;
using System.Text.Json;

namespace Shared.Models.Templates
{
    public struct StreamQualityTpl
    {
        public List<(string link, string quality)> data { get; set; } = new List<(string, string)>(8);

        public StreamQualityTpl() { }

        public StreamQualityTpl(IEnumerable<(string link, string quality)> streams) 
        {
            if (streams != null)
            {
                foreach (var item in streams)
                    Append(item.link, item.quality);
            }
        }

        public bool Any() => data.Any();

        public void Append(string link, string quality)
        {
            if (string.IsNullOrEmpty(quality))
                return;

            var eventResult = InvkEvent.StreamQuality(new EventStreamQuality(link, quality, prepend: false));
            if (eventResult.next.HasValue && !eventResult.next.Value)
                return;

            link = eventResult.link ?? link;

            if (!string.IsNullOrEmpty(link))
                data.Add((link, quality));
        }

        public void Insert(string link, string quality)
        {
            if (string.IsNullOrEmpty(quality))
                return;

            var eventResult = InvkEvent.StreamQuality(new EventStreamQuality(link, quality, prepend: true));
            if (eventResult.next.HasValue && !eventResult.next.Value)
                return;

            link = eventResult.link ?? link;

            if (!string.IsNullOrEmpty(link))
                data.Insert(0, (link, quality));
        }

        public string ToJson() => JsonSerializer.Serialize(ToObject());

        public Dictionary<string, string> ToObject(bool emptyToNull = false)
        {
            var result = new Dictionary<string, string>();
            foreach (var item in data)
                result.TryAdd(item.quality, item.link);

            if (emptyToNull && result.Count == 0)
                return null;

            return result;
        }

        public string MaxQuality()
        {
            if (data.Count == 0)
                return string.Empty;

            return data[0].quality;
        }

        public (string link, string quality) Firts()
        {
            if (data.Count == 0)
                return default;

            var eventResult = InvkEvent.StreamQualityFirts(new EventStreamQualityFirts(data));

            return eventResult ?? data[0];
        }
    }
}
