using Shared.Models.Events;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates;

public class StreamQualityTpl
{
    private readonly int _capacity;

    public List<StreamQualityDto> data { get; set; }

    public StreamQualityTpl(int capacity = 8)
    {
        _capacity = capacity;
    }

    public StreamQualityTpl(IEnumerable<(string link, string quality)> streams)
    {
        if (streams != null)
        {
            foreach (var item in streams)
                Append(item.link, item.quality);
        }
    }

    public StreamQualityTpl(IReadOnlyList<StreamQualityDto> streams, Func<string, string> linkPredicate = null)
    {
        if (streams != null)
        {
            _capacity = streams.Count;

            foreach (var item in streams)
            {
                string link = linkPredicate != null
                    ? linkPredicate(item.link)
                    : item.link;

                Append(link, item.quality);
            }
        }
    }


    public bool Any()
        => data?.Count > 0;

    public bool IsEmpty
        => data == null || data.Count == 0;

    public void Append(string link, string quality)
    {
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(quality))
            return;

        if (!TryProcessStreamQualityEvent(ref link, quality, prepend: false))
            return;

        if (!string.IsNullOrEmpty(link))
        {
            data ??= new List<StreamQualityDto>(_capacity);
            data.Add(new StreamQualityDto(link, quality));
        }
    }

    public void Insert(string link, string quality)
    {
        if (string.IsNullOrEmpty(link) || string.IsNullOrEmpty(quality))
            return;

        if (!TryProcessStreamQualityEvent(ref link, quality, prepend: true))
            return;

        if (!string.IsNullOrEmpty(link))
        {
            data ??= new List<StreamQualityDto>(_capacity);
            data.Insert(0, new StreamQualityDto(link, quality));
        }
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

    public string ToJson()
    {
        if (IsEmpty)
            return "{}";

        return JsonSerializer.Serialize(ToObject(), StreamQualityJsonContext.Default.DictionaryStringString);
    }

    public Dictionary<string, string> ToObject(bool emptyToNull = false)
    {
        if (emptyToNull && IsEmpty)
            return null;

        var result = new Dictionary<string, string>(data.Count);

        foreach (var item in data)
            result.TryAdd(item.quality, item.link);

        return result;
    }

    public string MaxQuality()
    {
        if (IsEmpty)
            return string.Empty;

        return data[0].quality;
    }

    public StreamQualityDto Firts()
    {
        if (IsEmpty)
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

public class StreamQualityDto
{
    public string link { get; }
    public string quality { get; }

    public StreamQualityDto(string link, string quality)
    {
        this.link = link;
        this.quality = quality;
    }
}
