using System.Text;

namespace Shared.Model.Templates
{
    public class MovieTpl
    {
        string? title, original_title;

        List<(string name, string link, string method, string stream, StreamQualityTpl streamquality, SubtitleTpl subtitles, string voice_name, string year, string details, string quality)> data = new List<(string, string, string, string, StreamQualityTpl, SubtitleTpl, string, string, string, string)>();

        public MovieTpl(string? title, string? original_title = null, int capacity = 0) 
        {
            this.title = title;
            this.original_title = original_title;
            data.Capacity = capacity; 
        }
        public bool IsEmpty() => data.Count == 0;

        public void Append(string? name, string? link, string method = "play", string? stream = null, StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, string? voice_name = null, string? year = null, string? details = null, string? quality = null)
        {
            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(link))
                data.Add((name, link, method, stream, streamquality, subtitles, voice_name, year, details, quality));
        }

        public string ToHtml(string? name, string? link, string method = "play", string? stream = null, StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, string? voice_name = null, string? year = null, string? details = null, string? quality = null)
        {
            Append(name, link, method, stream, streamquality, subtitles, voice_name, year, details, quality);
            return ToHtml();
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

                if (!string.IsNullOrEmpty(i.stream))
                    datajson.Append(",\"stream\":\"" + i.stream + "\"");

                if (i.streamquality != null && !i.streamquality.IsEmpty())
                    datajson.Append(",\"quality\": {" + i.streamquality.ToHtml() + "}");

                if (i.subtitles != null && !i.subtitles.IsEmpty())
                    datajson.Append(",\"subtitles\": [" + i.subtitles.ToHtml() + "]");

                if (!string.IsNullOrEmpty(i.voice_name))
                    datajson.Append(",\"voice_name\":\"" + i.voice_name + "\"");

                if (!string.IsNullOrEmpty(i.details))
                    datajson.Append(",\"details\":\"" + i.details + "\"");

                if (!string.IsNullOrEmpty(i.year))
                    datajson.Append(",\"year\":\"" + i.year + "\"");

                html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\""+i.method+"\",\"url\":\""+i.link+"\",\"title\":\""+$"{title ?? original_title} ({i.name})"+"\""+datajson.ToString()+"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">"+i.name+"</div></div>");
                firstjson = false;

                if (!string.IsNullOrEmpty(i.quality))
                    html.Append($"<!--{i.quality}p-->");
            }

            return html.ToString() + "</div>";
        }
    }
}
