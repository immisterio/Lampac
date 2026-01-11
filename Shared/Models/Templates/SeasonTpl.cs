using Shared.Engine.Pools;
using System.Text;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates
{
    public class SeasonTpl : ITplResult
    {
        public List<SeasonDto> data { get; private set; }

        public string quality { get; private set; }

        public VoiceTpl? vtpl { get; private set; }


        public SeasonTpl() : this(null, null, 10) { }

        public SeasonTpl(int capacity) : this(null, null, capacity) { }

        public SeasonTpl(string quality) : this(null, quality, 10) { }

        public SeasonTpl(string quality, int capacity) : this(null, quality, capacity) { }

        public SeasonTpl(VoiceTpl vtpl) : this(vtpl, null, 10) { }

        public SeasonTpl(VoiceTpl vtpl, int capacity) : this(vtpl, null, capacity) { }

        public SeasonTpl(VoiceTpl? vtpl, string quality, int capacity)
        {
            data = new List<SeasonDto>(capacity);
            this.vtpl = vtpl;
            this.quality = quality;
        }


        public void Append(string name, string link, string id)
        {
            int.TryParse(id, out int sid);
            Append(name, link, sid);
        }

        public void Append(string name, string link, int id)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add(new SeasonDto(link, name, id));
        }

        public void Append(VoiceTpl vtpl)
        {
            this.vtpl = vtpl;
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

            if (vtpl.HasValue)
                vtpl.Value.WriteTo(html);

            html.Append("<div class=\"videos__line\">");

            if (!string.IsNullOrEmpty(quality))
                html.Append($"<!--q:{quality}-->");

            foreach (var i in data)
            {
                html.Append("<div class=\"videos__item videos__season selector ");
                if (firstjson)
                    html.Append("focused");

                html.Append("\" data-json='{\"method\":\"link\",\"url\":\"");
                html.Append(i.url);
                html.Append("\"}'>");

                html.Append("<div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">");

                UtilsTpl.HtmlEncode(i.name, html);

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

            UtilsTpl.WriteJson(json, new SeasonResponseDto
            (
                quality,
                vtpl?.ToObject(emptyToNull: true),
                data
            ), SeasonJsonContext.Default.SeasonResponseDto);

            return json;
        }
    }


    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    )]
    [JsonSerializable(typeof(SeasonDto))]
    [JsonSerializable(typeof(VoiceDto))]
    [JsonSerializable(typeof(SeasonResponseDto))]
    [JsonSerializable(typeof(List<VoiceDto>))]
    [JsonSerializable(typeof(List<SeasonDto>))]
    public partial class SeasonJsonContext : JsonSerializerContext
    {
    }

    public readonly struct SeasonDto
    {
        public string method { get; }
        public int? id { get; }
        public string url { get; }
        public string name { get; }

        [JsonConstructor]
        public SeasonDto(string url, string name, int? id)
        {
            method = "link";
            this.id = id;
            this.url = url;
            this.name = name;
        }
    }

    public readonly struct SeasonResponseDto
    {
        public string type { get; }
        public string maxquality { get; }
        public IReadOnlyList<VoiceDto> voice { get; }
        public IReadOnlyList<SeasonDto> data { get; }

        [JsonConstructor]
        public SeasonResponseDto(string maxquality, IReadOnlyList<VoiceDto> voice, IReadOnlyList<SeasonDto> data)
        {
            type = "season";
            this.maxquality = maxquality;
            this.voice = voice;
            this.data = data;
        }
    }
}
