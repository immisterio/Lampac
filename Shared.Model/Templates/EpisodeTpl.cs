using System.Text;

namespace Shared.Model.Templates
{
    public class EpisodeTpl
    {
        List<(string name, string title, string s, string e, string link, string method, StreamQualityTpl? streamquality, SubtitleTpl? subtitles)> data = new List<(string, string, string, string, string, string, StreamQualityTpl, SubtitleTpl)>();

        public EpisodeTpl() { }

        public EpisodeTpl(int capacity) { data.Capacity = capacity; }

        public void Append(string name, string title, string s, string e, string link, string method = "play", StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add((name, title, s, e, link, method, streamquality, subtitles));
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
                var datajson = new StringBuilder();

                if (i.streamquality != null && !i.streamquality.IsEmpty())
                    datajson.Append(",\"quality\": {" + i.streamquality.ToHtml() + "}");

                if (i.subtitles != null && !i.subtitles.IsEmpty())
                    datajson.Append(",\"subtitles\": [" + i.subtitles.ToHtml() + "]");

                html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + i.s + "\" e=\"" + i.e + "\" data-json='{\"method\":\"" + i.method + "\",\"url\":\"" + i.link + "\",\"title\":\"" + i.title + "\""+datajson.ToString()+"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + i.name + "</div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }
    }
}
