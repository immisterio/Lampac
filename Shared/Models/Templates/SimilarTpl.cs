using System.Text;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates;

public class SimilarTpl : ITplResult
{
    public static string OnlineSplit => "<span class=\"online-prestige-split\">●</span>";

    public List<SimilarDto> data { get; set; }

    public SimilarTpl(int capacity = 20)
    {
        data = new List<SimilarDto>(capacity);
    }


    public void Append(string title, string year, string details, string link, string img = null)
    {
        if (!string.IsNullOrEmpty(title))
        {
            data.Add(new SimilarDto(
                link,
                year != null && short.TryParse(year, out short _year)
                    ? _year
                    : (short)0,
                details,
                title,
                img
            ));
        }
    }


    public bool IsEmpty
        => data == null || data.Count == 0;

    public int Length
        => data?.Count ?? 0;

    public object ToObject()
        => this;


    public string ToHtml()
    {
        if (IsEmpty)
            return string.Empty;

        var sb = ToBuilderHtml();

        try
        {
            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Return(sb);
        }
    }

    public StringBuilder ToBuilderHtml()
    {
        if (IsEmpty)
            return StringBuilderPool.EmptyHtml;

        var html = StringBuilderPool.Rent();

        bool firstjson = true;

        html.Append("<div class=\"videos__line\">");

        using (var utf8Buf = new BufferWriterPool<byte>())
        {
            foreach (var i in data)
            {
                html.Append("<div class=\"videos__item videos__season selector ");
                if (firstjson)
                    html.Append("focused");
                html.Append("\" ");

                html.Append("data-json='");
                UtilsTpl.WriteJson(html, utf8Buf, i, SimilarJsonContext.Default.SimilarDto);
                html.Append("'>");

                html.Append("<div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">");
                UtilsTpl.HtmlEncode(i.title, html);
                html.Append("</div></div></div>");

                firstjson = false;
            }
        }

        html.Append("</div>");

        return html;
    }


    public string ToJson()
    {
        if (IsEmpty)
            return string.Empty;

        var sb = ToBuilderJson();

        try
        {
            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Return(sb);
        }
    }

    public StringBuilder ToBuilderJson()
    {
        if (IsEmpty)
            return StringBuilderPool.EmptyJsonObject;

        var json = StringBuilderPool.Rent();

        using (var utf8Buf = new BufferWriterPool<byte>(largePool: true))
            UtilsTpl.WriteJson(json, utf8Buf, new SimilarResponseDto(data), SimilarJsonContext.Default.SimilarResponseDto);

        return json;
    }
}


[JsonSourceGenerationOptions(
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
)]
[JsonSerializable(typeof(SimilarDto))]
[JsonSerializable(typeof(SimilarResponseDto))]
public partial class SimilarJsonContext : JsonSerializerContext
{
}

public record SimilarDto
{
    public string method { get; }
    public string url { get; }
    public bool similar { get; }
    public short year { get; }
    public string details { get; }
    public string title { get; }
    public string img { get; }

    [JsonConstructor]
    public SimilarDto(
        string url,
        short year,
        string details,
        string title,
        string img
    )
    {
        method = "link";
        this.url = url;
        similar = true;
        this.year = year;
        this.details = details;
        this.title = title;
        this.img = img;
    }
}

public record SimilarResponseDto
{
    public string type { get; }
    public IReadOnlyList<SimilarDto> data { get; }

    [JsonConstructor]
    public SimilarResponseDto(IReadOnlyList<SimilarDto> data)
    {
        type = "similar";
        this.data = data;
    }
}
