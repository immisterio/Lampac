using Shared.Model.Base;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Model.Templates
{
    public static class VideoTpl
    {
        public static string ToJson(string method, string url, string title, StreamQualityTpl? streamquality = null, SubtitleTpl? subtitles = null, VastConf? vast = null)
        {
            return JsonSerializer.Serialize(new
            {
                title,
                method,
                url,
                quality = streamquality?.ToObject(),
                subtitles = subtitles?.ToObject(),
                vast_url = vast?.url ?? AppInit._vast?.url,
                vast_msg = vast?.msg ?? AppInit._vast?.msg

            }, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault });
        }
    }
}
