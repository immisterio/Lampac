using Shared.Engine.Pools;
using System.Text;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates
{
    public struct VoiceTpl
    {
        public List<VoiceDto> data { get; private set; }

        public VoiceTpl() : this(20) { }

        public VoiceTpl(int capacity) 
        { 
            data = new List<VoiceDto>(capacity); 
        }

        public bool IsEmpty => data == null || data.Count == 0;

        public void Append(string name, bool active, string link)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add(new VoiceDto(link, active, name));
        }

        public string ToHtml()
        {
            if (IsEmpty)
                return string.Empty;

            var sb = StringBuilderPool.Rent();
            WriteTo(sb);

            StringBuilderPool.Return(sb);
            return sb.ToString();
        }

        public void WriteTo(StringBuilder sb)
        {
            if (IsEmpty)
                return;

            sb.Append("<div class=\"videos__line\">");

            foreach (var i in data)
            {
                sb.Append("<div class=\"videos__button selector ");
                if (i.active)
                    sb.Append("active");

                sb.Append("\" data-json='{\"method\":\"link\",\"url\":\"");
                sb.Append(i.url);
                sb.Append("\"}'>");

                UtilsTpl.HtmlEncode(i.name, sb);

                sb.Append("</div>");
            }

            sb.Append("</div>");
        }

        public IReadOnlyList<VoiceDto> ToObject(bool emptyToNull = false)
        {
            if (IsEmpty)
                return emptyToNull ? null : Array.Empty<VoiceDto>();

            return data;
        }
    }


    public readonly struct VoiceDto
    {
        public string method { get; }
        public string url { get; }
        public bool active { get; }
        public string name { get; }

        [JsonConstructor]
        public VoiceDto(string url, bool active, string name)
        {
            method = "link";
            this.url = url;
            this.active = active;
            this.name = name;
        }
    }
}
