using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Shared.Models.Templates
{
    public class SeasonTpl : ITplResult
    {
        static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault };

        static readonly ThreadLocal<StringBuilder> sb = new(() => new StringBuilder(50_000));


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

        public string ToHtml()
            => ToBuilderHtml().ToString();

        public StringBuilder ToBuilderHtml()
        {
            var html = sb.Value;
            html.Clear();

            if (IsEmpty)
                return html;

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

                UtilsTpl.HtmlEncode(i.name.AsSpan(), html);

                html.Append("</div></div></div>");

                firstjson = false;
            }

            html.Append("</div>");

            return html;
        }

        public string ToJson()
            => ToBuilderJson().ToString();

        public StringBuilder ToBuilderJson()
        {
            var json = sb.Value;
            json.Clear();

            if (IsEmpty)
            {
                json.Append("{}");
                return json;
            }

            UtilsTpl.WriteJson(json, new SeasonResponseDto
            (
                quality,
                vtpl?.ToObject(emptyToNull: true),
                data
            ), jsonOptions);

            return json;
        }
    }


    public readonly struct SeasonDto
    {
        public string method { get; }
        public int? id { get; }
        public string url { get; }
        public string name { get; }

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

        public SeasonResponseDto(string maxquality, IReadOnlyList<VoiceDto> voice, IReadOnlyList<SeasonDto> data)
        {
            type = "season";
            this.maxquality = maxquality;
            this.voice = voice;
            this.data = data;
        }
    }
}
