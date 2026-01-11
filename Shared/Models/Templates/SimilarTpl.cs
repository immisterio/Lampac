using Shared.Engine.Pools;
using System.Text;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates
{
    public class SimilarTpl : ITplResult
    {
        public static string OnlineSplit => "<span class=\"online-prestige-split\">●</span>";


        public List<SimilarDto> data { get; private set; }


        public SimilarTpl() : this(20) { }

        public SimilarTpl(int capacity) 
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


        public bool IsEmpty => data == null || data.Count == 0;

        public int Length => data?.Count ?? 0;


        public string ToHtml()
        {
            if (IsEmpty)
                return string.Empty;

            var sb = ToBuilderHtml();
            string result = sb.ToString();

            StringBuilderPool.Return(sb);
            return result;
        }

        public StringBuilder ToBuilderHtml()
        {
            if (IsEmpty)
                return StringBuilderPool.EmptyHtml;

            var html = StringBuilderPool.Rent();

            bool firstjson = true;

            html.Append("<div class=\"videos__line\">");

            foreach (var i in data)
            {
                html.Append("<div class=\"videos__item videos__season selector ");
                if (firstjson)
                    html.Append("focused");
                html.Append("\" ");

                html.Append("data-json='");
                UtilsTpl.WriteJson(html, i, SimilarJsonContext.Default.SimilarDto);
                html.Append("'>");

                html.Append("<div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">");
                UtilsTpl.HtmlEncode(i.title, html);
                html.Append("</div></div></div>");

                firstjson = false;
            }

            html.Append("</div>");

            return html;
        }


        public string ToJson()
        {
            if (IsEmpty)
                return string.Empty;

            var sb = ToBuilderJson();
            string result = sb.ToString();

            StringBuilderPool.Return(sb);
            return result;
        }

        public StringBuilder ToBuilderJson()
        {
            if (IsEmpty)
                return StringBuilderPool.EmptyJsonObject;

            var json = StringBuilderPool.Rent();

            UtilsTpl.WriteJson(json, new SimilarResponseDto(data), SimilarJsonContext.Default.SimilarResponseDto);

            return json;
        }
    }


    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    )]
    [JsonSerializable(typeof(SimilarDto))]
    [JsonSerializable(typeof(SimilarResponseDto))]
    [JsonSerializable(typeof(List<SimilarDto>))]
    public partial class SimilarJsonContext : JsonSerializerContext
    {
    }

    public readonly struct SimilarDto
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

    public readonly struct SimilarResponseDto
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
}
