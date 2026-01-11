using Shared.Engine;
using Shared.Engine.Pools;
using Shared.Models.Base;
using System.Text;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates
{
    public class MovieTpl : ITplResult
    {
        string title, original_title;

        public VoiceTpl? vtpl { get; private set; }

        public List<(string voiceOrQuality, string link, string method, string stream, StreamQualityTpl? streamquality, SubtitleTpl? subtitles, string voice_name, string year, string details, string quality, VastConf vast, List<HeadersModel> headers, int? hls_manifest_timeout, SegmentTpl? segments, string subtitles_call)> data { get; private set; }


        public MovieTpl(string title) : this(title, null, 15) { }

        public MovieTpl(string title, string original_title) : this(title, original_title, 15) { }

        public MovieTpl(string title, string original_title, int capacity) : this(title, original_title, null, capacity) { }

        public MovieTpl(string title, string original_title, in VoiceTpl vtpl) : this(title, original_title, vtpl, 15) { }

        public MovieTpl(string title, string original_title, in VoiceTpl? vtpl, int capacity)
        {
            this.vtpl = vtpl;
            this.title = title;
            this.original_title = original_title;
            data = new List<(string, string, string, string, StreamQualityTpl?, SubtitleTpl?, string, string, string, string, VastConf vast, List<HeadersModel>, int?, SegmentTpl?, string)>(capacity);
        }


        public void Append(string voiceOrQuality, string link, string method = "play", string stream = null, in StreamQualityTpl? streamquality = null, in SubtitleTpl? subtitles = null, string voice_name = null, string year = null, string details = null, string quality = null, VastConf vast = null, List<HeadersModel> headers = null, int? hls_manifest_timeout = null, in SegmentTpl? segments = null, string subtitles_call = null)
        {
            if (!string.IsNullOrEmpty(voiceOrQuality) && !string.IsNullOrEmpty(link))
                data.Add((voiceOrQuality, link, method, stream, streamquality, subtitles, voice_name, year, details, quality, vast, headers, hls_manifest_timeout, segments, subtitles_call));
        }

        public void Append(in VoiceTpl vtpl)
        {
            this.vtpl = vtpl;
        }


        public bool IsEmpty => data == null || data.Count == 0;

        public int Length => data?.Count ?? 0;


        public void Reverse()
        {
            data.Reverse();
        }

        public string ToHtml()
        {
            if (IsEmpty)
                return string.Empty;

            var sb = ToBuilderHtml();
            string result = sb.ToString();

            StringBuilderPool.Return(sb);
            return result;
        }

        public StringBuilder ToBuilderHtml()
        {
            if (IsEmpty)
                return StringBuilderPool.EmptyHtml;

            var html = StringBuilderPool.Rent();

            bool firstjson = true;

            if (vtpl.HasValue)
                vtpl.Value.WriteTo(html);

            html.Append("<div class=\"videos__line\">");

            foreach (var i in data)
            {
                var vast = i.vast ?? AppInit.conf.vast;

                html.Append("<div class=\"videos__item videos__movie selector ");
                if (firstjson)
                    html.Append("focused");
                html.Append("\" ");

                html.Append("media=\"\" ");

                html.Append("data-json='");
                UtilsTpl.WriteJson(html, new MovieDto
                (
                    i.method,
                    i.link,
                    i.stream,
                    Http.NormalizeHeaders(i.headers),
                    i.streamquality?.ToObject(emptyToNull: true),
                    i.subtitles?.ToObject(emptyToNull: true),
                    i.subtitles_call,
                    i.voiceOrQuality,
                    i.streamquality?.MaxQuality() ?? i.quality,
                    i.voice_name,
                    i.details,
                    int.TryParse(i.year, out int _year) ? _year : 0,
                    $"{title ?? original_title} ({i.voiceOrQuality})",
                    i.hls_manifest_timeout,
                    vast?.url != null ? vast : null,
                    i.segments?.ToObject()
                ), MovieJsonContext.Default.MovieDto);
                html.Append("'>");

                html.Append("<div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">");
                UtilsTpl.HtmlEncode(i.voiceOrQuality, html);
                html.Append("</div></div>");

                if (!string.IsNullOrEmpty(i.quality))
                {
                    if (i.quality.EndsWith("p"))
                        html.Append($"<!--{i.quality}-->");
                    else
                        html.Append($"<!--{i.quality}p-->");
                }

                firstjson = false;
            }

            html.Append("</div>");

            return html;
        }

        public string ToJson()
        {
            if (IsEmpty)
                return string.Empty;

            var sb = ToBuilderJson();
            string result = sb.ToString();

            StringBuilderPool.Return(sb);
            return result;
        }

        public StringBuilder ToBuilderJson()
        {
            if (IsEmpty)
                return StringBuilderPool.EmptyJsonObject;

            var json = StringBuilderPool.Rent();

            var arr = new MovieDto[data.Count];

            for (int idx = 0; idx < data.Count; idx++)
            {
                var i = data[idx];
                var vast = i.vast ?? AppInit.conf.vast;

                arr[idx] = new MovieDto(
                    i.method,
                    i.link,
                    i.stream,
                    Http.NormalizeHeaders(i.headers),
                    i.streamquality?.ToObject(emptyToNull: true),
                    i.subtitles?.ToObject(emptyToNull: true),
                    i.subtitles_call,
                    i.voiceOrQuality,
                    i.streamquality?.MaxQuality() ?? i.quality,
                    (i.voice_name == null && i.details == null) ? null : (i.voice_name + i.details),
                    null,
                    int.TryParse(i.year, out int _year) ? _year : 0,
                    $"{title ?? original_title} ({i.voiceOrQuality})",
                    i.hls_manifest_timeout,
                    vast?.url != null ? vast : null,
                    i.segments?.ToObject()
                );
            }

            UtilsTpl.WriteJson(json, new MovieResponseDto
            (
                vtpl?.ToObject(emptyToNull: true),
                arr
            ), MovieJsonContext.Default.MovieResponseDto);

            return json;
        }
    }


    [JsonSourceGenerationOptions(
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    )]
    [JsonSerializable(typeof(MovieDto))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(SubtitleDto))]
    [JsonSerializable(typeof(List<SubtitleDto>))]
    [JsonSerializable(typeof(VastConf))]
    [JsonSerializable(typeof(SegmentDto))]
    [JsonSerializable(typeof(List<SegmentDto>))]
    [JsonSerializable(typeof(Dictionary<string, List<SegmentDto>>))]
    [JsonSerializable(typeof(List<MovieDto>))]
    [JsonSerializable(typeof(MovieDto[]))]
    [JsonSerializable(typeof(VoiceDto))]
    [JsonSerializable(typeof(List<VoiceDto>))]
    [JsonSerializable(typeof(MovieResponseDto))]
    public partial class MovieJsonContext : JsonSerializerContext
    {
    }

    public readonly struct MovieDto
    {
        public string method { get; }
        public string url { get; }
        public string stream { get; }
        public Dictionary<string, string> headers { get; }
        public Dictionary<string, string> quality { get; }
        public IReadOnlyList<SubtitleDto> subtitles { get; }
        public string subtitles_call { get; }
        public string translate { get; }
        public string maxquality { get; }
        public string voice_name { get; }
        public string details { get; }
        public int year { get; }
        public string title { get; }
        public int? hls_manifest_timeout { get; }
        public VastConf vast { get; }
        public Dictionary<string, IReadOnlyList<SegmentDto>> segments { get; }

        [JsonConstructor]
        public MovieDto(
            string method,
            string url,
            string stream,
            Dictionary<string, string> headers,
            Dictionary<string, string> quality,
            IReadOnlyList<SubtitleDto> subtitles,
            string subtitles_call,
            string translate,
            string maxquality,
            string voice_name,
            string details,
            int year,
            string title,
            int? hls_manifest_timeout,
            VastConf vast,
        Dictionary<string, IReadOnlyList<SegmentDto>> segments)
        {
            this.method = method;
            this.url = url;
            this.stream = stream;
            this.headers = headers;
            this.quality = quality;
            this.subtitles = subtitles;
            this.subtitles_call = subtitles_call;
            this.translate = translate;
            this.maxquality = maxquality;
            this.voice_name = voice_name;
            this.details = details;
            this.year = year;
            this.title = title;
            this.hls_manifest_timeout = hls_manifest_timeout;
            this.vast = vast;
            this.segments = segments;
        }
    }

    public readonly struct MovieResponseDto
    {
        public string type { get; }
        public IReadOnlyList<VoiceDto> voice { get; }
        public MovieDto[] data { get; }

        [JsonConstructor]
        public MovieResponseDto(IReadOnlyList<VoiceDto> voice, MovieDto[] data)
        {
            type = "movie";
            this.voice = voice;
            this.data = data;
        }
    }
}
