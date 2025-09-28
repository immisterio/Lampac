using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Shared.Models.Templates
{
    public struct SimilarTpl
    {
        public List<(string title, string year, string details, string link, string img)> data { get; set; }

        public SimilarTpl() : this(20) { }

        public SimilarTpl(int capacity) { data = new List<(string, string, string, string, string)>(capacity); }


        public string OnlineSplit => "{prestige-split}";


        public void Append(string title, string year, string details, string link, string img = null)
        {
            if (!string.IsNullOrEmpty(title))
                data.Add((title, year, details, link, img));
        }

        public string ToHtml()
        {
            if (data.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            foreach (var i in data) 
            {
                string datajson = JsonSerializer.Serialize(new
                {
                    method = "link",
                    url = i.link,
                    similar = true,
                    year = i.year != null && int.TryParse(i.year, out int _year) ? _year : 0,
                    i.details,
                    i.img

                }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });

                datajson = datajson.Replace("{prestige-split}", "<span class=\\\"online-prestige-split\\\">●</span>");

                html.Append($"<div class=\"videos__item videos__season selector {(firstjson ? "focused" : "")}\" data-json='{datajson}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">{HttpUtility.HtmlEncode(i.title)}</div></div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }


        public string ToJson()
        {
            if (data.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(new 
            {
                type = "similar",
                data = data.Select(i => new 
                {
                    url = i.link,
                    details = i.details?.Replace("{prestige-split}", "<span class=\"online-prestige-split\">●</span>"),
                    i.title,
                    year = int.TryParse(i.year, out int _year) ? _year : 0,
                    i.img
                })
            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        }
    }
}
