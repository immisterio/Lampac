﻿using System.Text.Json;

namespace Shared.Model.Templates
{
    public struct StreamQualityTpl
    {
        List<(string link, string quality)> data = new List<(string, string)>(8);

        public StreamQualityTpl() { }

        public StreamQualityTpl(IEnumerable<(string link, string quality)> streams) { if (streams != null) data = streams.ToList(); }

        public bool Any() => data.Any();

        public void Append(in string link, in string quality)
        {
            if (!string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(quality))
                data.Add((link, quality));
        }

        public void Insert(in string link, in string quality)
        {
            if (!string.IsNullOrEmpty(link) && !string.IsNullOrEmpty(quality))
                data.Insert(0, (link, quality));
        }

        public string ToJson() => JsonSerializer.Serialize(ToObject());

        public Dictionary<string, string> ToObject()
        {
            var result = new Dictionary<string, string>();
            foreach (var item in data)
                result.TryAdd(item.quality, item.link);

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

            return data[0];
        }
    }
}
