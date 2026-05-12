using Shared.Models.Events;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates;

public class StreamQualityTpl
{
    public List<StreamQualityDto> data { get; set; }

    public StreamQualityTpl(int capacity = 8)
    {
        data = new List<StreamQualityDto>(capacity);
    }

    public StreamQualityTpl(IEnumerable<(string link, string quality)> streams)
    {
        data = new List<StreamQualityDto>(8);

        if (streams != null)
        {
            foreach (var item in streams)
                Append(item.link, item.quality);
        }
    }

    public StreamQualityTpl(IReadOnlyList<StreamQualityDto> streams, Func<string, string> linkPredicate = null)
    {
        data = new List<StreamQualityDto>(streams?.Count ?? 8);

        if (streams != null)
        {
            foreach (var item in streams)
            {
                string link = linkPredicate != null
                    ? linkPredicate(item.link)
                    : item.link;

                Append(link, item.quality);
            }
        }
    }


    public bool Any() => data.Any();

    public void Append(string link, string quality)
    {
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(quality))
            return;

        if (!TryProcessStreamQualityEvent(ref link, quality, prepend: false))
            return;

        if (!string.IsNullOrEmpty(link))
            data.Add(new StreamQualityDto(link, quality));
    }

    public void Insert(string link, string quality)
    {
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(quality))
            return;

        if (!TryProcessStreamQualityEvent(ref link, quality, prepend: true))
            return;

        if (!string.IsNullOrEmpty(link))
            data.Insert(0, new StreamQualityDto(link, quality));
    }

    private static bool TryProcessStreamQualityEvent(ref string link, string quality, bool prepend)
    {
        var streamQualityHandlers = EventListener.StreamQuality;
        if (streamQualityHandlers == null)
            return true;

        var em = new EventStreamQuality(link, quality, prepend);

        foreach (Func<EventStreamQuality, (bool? next, string link)> handler in streamQualityHandlers.GetInvocationList())
        {
            var eventResult = handler(em);

            link = eventResult.link ?? link;

            if (eventResult.next.HasValue && !eventResult.next.Value)
                return false;
        }

        return true;
    }

    public string ToJson() => JsonSerializer.Serialize(ToObject(), StreamQualityJsonContext.Default.DictionaryStringString);

    public Dictionary<string, string> ToObject(bool emptyToNull = false)
    {
        if (emptyToNull && data.Count == 0)
            return null;

        var result = new Dictionary<string, string>(data.Count);

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

    public StreamQualityDto Firts()
    {
        if (data.Count == 0)
            return default;

        if (EventListener.StreamQualityFirts != null)
        {
            var em = new EventStreamQualityFirts(data);
            foreach (Func<EventStreamQualityFirts, StreamQualityDto> handler in EventListener.StreamQualityFirts.GetInvocationList())
            {
                var eventResult = handler.Invoke(em);
                if (eventResult != null)
                    return eventResult;
            }
        }

        return data[0];
    }
}


[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
)]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class StreamQualityJsonContext : JsonSerializerContext
{
}

public record StreamQualityDto
{
    public string link { get; }
    public string quality { get; }

    public StreamQualityDto(string link, string quality)
    {
        this.link = link;
        this.quality = quality;
    }
}
