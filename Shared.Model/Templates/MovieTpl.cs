﻿using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Web;

namespace Shared.Model.Templates
{
    public class MovieTpl
    {
        string? title, original_title;

        List<(string? voiceOrQuality, string? link, string method, string? stream, StreamQualityTpl? streamquality, SubtitleTpl? subtitles, string? voice_name, string? year, string? details, string? quality, string? vast_url)> data = new List<(string?, string?, string, string?, StreamQualityTpl?, SubtitleTpl?, string?, string?, string?, string?, string?)>();

        public MovieTpl(string? title, string? original_title = null, int capacity = 0) 
        {
            this.title = title;
            this.original_title = original_title;
            data.Capacity = capacity; 
        }
        public bool IsEmpty() => data.Count == 0;

        public void Append(string? voiceOrQuality, string? link, string method = "play", string? stream = null, StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, string? voice_name = null, string? year = null, string? details = null, string? quality = null, string? vast_url = null)
        {
            if (!string.IsNullOrEmpty(voiceOrQuality) && !string.IsNullOrEmpty(link))
                data.Add((voiceOrQuality, link, method, stream, streamquality, subtitles, voice_name, year, details, quality, vast_url));
        }

        public string ToHtml(string? voiceOrQuality, string? link, string method = "play", string? stream = null, StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, string? voice_name = null, string? year = null, string? details = null, string? quality = null, string? vast_url = null)
        {
            Append(voiceOrQuality, link, method, stream, streamquality, subtitles, voice_name, year, details, quality, vast_url);
            return ToHtml();
        }

        public string ToHtml(bool reverse = false)
        {
            if (data.Count == 0)
                return string.Empty;

            bool firstjson = true;
            var html = new StringBuilder();
            html.Append("<div class=\"videos__line\">");

            if (reverse)
                data.Reverse();

            foreach (var i in data) 
            {
                string datajson = JsonSerializer.Serialize(new
                {
                    i.method,
                    url = i.link,
                    i.stream,
                    quality = i.streamquality?.ToObject(),
                    subtitles = i.subtitles?.ToObject(),
                    translate = i.voiceOrQuality,
                    maxquality = i.streamquality?.MaxQuality() ?? i.quality,
                    i.voice_name,
                    i.details,
                    i.vast_url,
                    year = int.TryParse(i.year, out int _year) ? _year : 0,
                    title = $"{title ?? original_title} ({i.voiceOrQuality})",

                }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });

                html.Append($"<div class=\"videos__item videos__movie selector {(firstjson ? "focused" : "")}\" media=\"\" data-json='{datajson}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">{HttpUtility.HtmlEncode(i.voiceOrQuality)}</div></div>");
                firstjson = false;

                if (!string.IsNullOrEmpty(i.quality))
                    html.Append($"<!--{i.quality}p-->");
            }

            return html.ToString() + "</div>";
        }

        public string ToJson(bool reverse = false, VoiceTpl? vtpl = null)
        {
            if (data.Count == 0)
                return "[]";

            if (reverse)
                data.Reverse();

            return JsonSerializer.Serialize(new
            {
                type = "movie",
                voice = vtpl?.ToObject(),
                data = data.Select(i => new
                {
                    i.method,
                    url = i.link,
                    i.stream,
                    quality = i.streamquality?.ToObject(),
                    subtitles = i.subtitles?.ToObject(),
                    translate = i.voiceOrQuality,
                    maxquality = i.streamquality?.MaxQuality() ?? i.quality,
                    details = (i.voice_name == null && i.details == null) ? null : (i.voice_name + i.details),
                    year = int.TryParse(i.year, out int _year) ? _year : 0,
                    title = $"{title ?? original_title} ({i.voiceOrQuality})",
                })
            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        }
    }
}
