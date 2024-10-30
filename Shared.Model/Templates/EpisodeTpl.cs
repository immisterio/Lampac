using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Model.Templates
{
    public class EpisodeTpl
    {
        List<(string name, string title, string s, string e, string link, string method, StreamQualityTpl? streamquality, SubtitleTpl? subtitles, string? streamlink, string? voice_name)> data = new List<(string, string, string, string, string, string, StreamQualityTpl, SubtitleTpl, string, string)>();

        public EpisodeTpl() { }

        public EpisodeTpl(int capacity) { data.Capacity = capacity; }

        public void Append(string name, string? title, string s, string e, string link, string method = "play", StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, string? streamlink = null, string? voice_name = null)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add((name, $"{title} ({e} серия)", s, e, link, method, streamquality, subtitles, streamlink, voice_name));
        }

        static string? fixName(string? _v) => _v?.Replace("\"", "%22")?.Replace("'", "%27");

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

                if (i.streamlink != null)
                    datajson.Append($",\"stream\": \"{i.streamlink}\"");

                if (i.streamquality != null && !i.streamquality.IsEmpty())
                    datajson.Append(",\"quality\": {" + i.streamquality.ToHtml() + "}");

                if(!string.IsNullOrEmpty(i.voice_name))
                    datajson.Append(",\"voice_name\":\"" + fixName(i.voice_name) + "\"");

                if (i.subtitles != null && !i.subtitles.IsEmpty())
                    datajson.Append(",\"subtitles\": [" + i.subtitles.ToHtml() + "]");

                html.Append("<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + i.s + "\" e=\"" + i.e + "\" data-json='{\"method\":\"" + i.method + "\",\"url\":\"" + i.link + "\",\"title\":\"" + fixName(i.title) + "\""+datajson.ToString()+"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + i.name + "</div></div>");
                firstjson = false;
            }

            return html.ToString() + "</div>";
        }

        public string ToJson(VoiceTpl? vtpl = null)
        {
            if (data.Count == 0)
                return "[]";

            return JsonSerializer.Serialize(new
            {
                type = "episode",
                voice = vtpl?.ToObject(),
                data = data.Select(i => new
                {
                    i.method,
                    url = i.link,
                    stream = i.streamlink,
                    quality = i.streamquality?.ToObject(),
                    subtitles = i.subtitles?.ToObject(),
                    s = int.TryParse(i.s, out _) ? int.Parse(i.s) : 0,
                    e = int.TryParse(i.e, out _) ? int.Parse(i.e) : 0,
                    details = i.voice_name,
                    i.name,
                    i.title
                })
            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        }
    }
}
