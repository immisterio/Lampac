using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Shared.Models.Templates
{
    public struct SeasonTpl
    {
        public List<(string name, string link, int? id)> data { get; set; }

        public string quality = null;

        public SeasonTpl() : this(10) { }

        public SeasonTpl(int capacity) { data = new List<(string, string, int?)>(capacity); }

        public SeasonTpl(string quality, int capacity = 10) 
        {
            data = new List<(string, string, int?)>(capacity);
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
                data.Add((name, link, id));
        }

        public string ToHtml(in VoiceTpl? vtpl = null)
        {
            if (data.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();

            if (vtpl != null)
                html.Append(vtpl?.ToHtml());

            html.Append("<div class=\"videos__line\">");

            if (!string.IsNullOrEmpty(quality))
                html.Append($"<!--q:{quality}-->");

            foreach (var i in data) 
            {
                html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + i.link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + HttpUtility.HtmlEncode(i.name) + "</div></div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }

        public string ToJson(in VoiceTpl? vtpl = null)
        {
            if (data.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(new
            {
                type = "season",
                maxquality = quality,
                voice = vtpl?.ToObject(),
                data = data.Select(i => new
                {
                    i.id,
                    method = "link",
                    url = i.link,
                    i.name
                })
            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        }
    }
}
