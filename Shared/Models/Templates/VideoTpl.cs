using Shared.Models.Base;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Models.Templates
{
    public static class VideoTpl
    {
        public static string ToJson(string method, string url, string title, in StreamQualityTpl? streamquality = null, in SubtitleTpl? subtitles = null, string quality = null, VastConf vast = null, List<HeadersModel> headers = null, int? hls_manifest_timeout = null, SegmentTpl? segments = null, string subtitles_call = null)
        {
            var _vast = vast ?? AppInit.conf.vast;

            return JsonSerializer.Serialize(new
            {
                title,
                method,
                url,
                headers = headers != null ? headers.ToDictionary(k => k.name, v => v.val) : null,
                quality = streamquality?.ToObject(emptyToNull: true) ?? new StreamQualityTpl(new List<(string, string)>() { (url, quality??"auto") }).ToObject(),
                subtitles = subtitles?.ToObject(emptyToNull: true),
                subtitles_call,
                hls_manifest_timeout,
                vast = _vast?.url != null ? _vast : _vast,
                segments = segments?.ToObject()

            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        }
    }
}
