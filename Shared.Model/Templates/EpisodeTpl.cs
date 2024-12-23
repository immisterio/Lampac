﻿using Shared.Model.Online;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Shared.Model.Templates
{
    public class EpisodeTpl
    {
        List<(string name, string title, string s, string e, string link, string method, StreamQualityTpl? streamquality, SubtitleTpl? subtitles, string? streamlink, string? voice_name, string? vast_url, string? vast_msg, List<HeadersModel>? headers)> data = new List<(string, string, string, string, string, string, StreamQualityTpl?, SubtitleTpl?, string?, string?, string?, string?, List<HeadersModel>?)>();

        public EpisodeTpl() { }

        public EpisodeTpl(int capacity) { data.Capacity = capacity; }

        public void Append(string name, string? title, string s, string e, string link, string method = "play", StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, string? streamlink = null, string? voice_name = null, string? vast_url = null, string? vast_msg = null, List<HeadersModel>? headers = null)
        {
            if (!string.IsNullOrEmpty(name))
                data.Add((name, $"{title} ({e} серия)", s, e, link, method, streamquality, subtitles, streamlink, voice_name, vast_url, vast_msg, headers));
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
                    i.method,
                    url = i.link,
                    i.title,
                    stream = i.streamlink,
                    headers = i.headers != null ? i.headers.ToDictionary(k => k.name, v => v.val) : null,
                    quality = i.streamquality?.ToObject(),
                    subtitles = i.subtitles?.ToObject(),
                    i.voice_name,
                    vast_url = i.vast_url ?? AppInit._vast?.url,
                    vast_msg = i.vast_msg ?? AppInit._vast?.msg

                }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });

                html.Append($"<div class=\"videos__item videos__movie selector {(firstjson ? "focused" : "")}\" media=\"\" s=\"{i.s}\" e=\"{i.e}\" data-json='{datajson}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">{HttpUtility.HtmlEncode(i.name)}</div></div>");
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
                    headers = i.headers != null ? i.headers.ToDictionary(k => k.name, v => v.val) : null,
                    quality = i.streamquality?.ToObject(),
                    subtitles = i.subtitles?.ToObject(),
                    s = int.TryParse(i.s, out int _s) ? _s : 0,
                    e = int.TryParse(i.e, out int _e) ? _e : 0,
                    details = i.voice_name,
                    vast_url = i.vast_url ?? AppInit._vast?.url,
                    vast_msg = i.vast_msg ?? AppInit._vast?.msg,
                    i.name,
                    i.title
                })
            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
        }
    }
}
