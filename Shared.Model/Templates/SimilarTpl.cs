using System.Text;

namespace Shared.Model.Templates
{
    public class SimilarTpl
    {
        List<(string title, string year, string details, string link)> data = new List<(string, string, string, string)>();

        public SimilarTpl() { }

        public SimilarTpl(int capacity) { data.Capacity = capacity; }


        public string OnlineSplit => "<span class=\\\"online-prestige-split\\\">●</span>";


        public void Append(string? title, string year, string details, string link)
        {
            if (!string.IsNullOrEmpty(title))
                data.Add((title, year, details, link));
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
                html.Append("<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\""+i.link+"\",\"similar\":true,\"year\":"+i.year+",\"details\":\""+i.details + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">"+i.title+"</div></div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }
    }
}
