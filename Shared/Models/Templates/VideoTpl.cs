using Lampac;
using Shared.Model.Base;
using Shared.Model.Online;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Model.Templates
{
    public static class VideoTpl
    {
        public static string ToJson(in string method, in string url, in string title, in StreamQualityTpl? streamquality = null, in SubtitleTpl? subtitles = null, in string quality = null, in VastConf vast = null, List<HeadersModel> headers = null, in int? hls_manifest_timeout = null)
        {
            return JsonSerializer.Serialize(new
            {
                title,
                method,
                url,
                headers = headers != null ? headers.ToDictionary(k => k.name, v => v.val) : null,
                quality = streamquality?.ToObject() ?? new StreamQualityTpl(new List<(string, string)>() { (url, quality??"auto") }).ToObject(),
                subtitles = subtitles?.ToObject(),
                vast_url = (vast?.url ?? AppInit.conf.vast?.url)?.Replace("{random}", DateTime.Now.ToFileTime().ToString()),
                vast_msg = vast?.msg ?? AppInit.conf.vast?.msg,
                hls_manifest_timeout

            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        }
    }
}
