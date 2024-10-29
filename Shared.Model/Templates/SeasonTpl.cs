using System.Text;
using System.Text.Json;

namespace Shared.Model.Templates
{
    public class SeasonTpl
    {
        #region SeasonTpl
        string? quality = null;

        public SeasonTpl() { }

        public SeasonTpl(string? quality) { this.quality = quality; }
        #endregion

        List<(string name, string link, int? id)> data = new List<(string, string, int?)>();

        public SeasonTpl(int capacity) { data.Capacity = capacity; }

        public void Append(string? name, string link, int? id = null)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add((name, link, id));
        }

        public string ToHtml()
        {
            if (data.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (!string.IsNullOrEmpty(quality))
                html.Append($"<!--q:{quality}-->");

            foreach (var i in data) 
            {
                html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + i.link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + i.name + "</div></div></div>");
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
                type = "season",
                maxquality = quality,
                data = data.Select(i => new
                {
                    i.id,
                    url = i.link,
                    i.name
                })
            });
        }
    }
}
