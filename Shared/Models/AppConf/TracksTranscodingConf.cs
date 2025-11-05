using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Shared.Models.AppConf
{
    public class TranscodingConf
    {
        public bool enable { get; set; }

        public string ffmpeg { get; set; }

        public string tempRoot { get; set; }

        public int idleTimeoutSec { get; set; }

        public int idleTimeoutSec_live { get; set; }

        public bool defaultSubtitles { get; set; }

        public int maxConcurrentJobs { get; set; }

        public string[] allowHosts { get; set; } = Array.Empty<string>();

        public TranscodingHlsOptions hlsOptions { get; set; } = new();

        public TranscodingAudioOptions audioOptions { get; set; } = new();

        public TranscodingSubtitleOptions subtitleOptions { get; set; } = new();

        public TranscodingPlaylistOptions playlistOptions { get; set; } = new();

        public TranscodingConvertOptions convertOptions { get; set; } = new();

        [JsonProperty("comand", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string[]> comand { get; set; } = new Dictionary<string, string[]>()
        {
            ["demuxer"] = ["-threads 0", "-fflags +genpts"],
            ["input"] = ["-avoid_negative_ts disabled"],
            ["output"] = [
                "-map 0:v:0", "-map 0:a:{audio_index}",
                "-dn", "-sn",
                "-map_metadata -1", "-map_chapters -1", "-max_muxing_queue_size 2048"
            ],
        };
    }

    public class TranscodingHlsOptions
    {
        [JsonIgnore]
        public int seek { get; set; }

        /// <summary>
        /// hls_time
        /// </summary>
        public int segDur { get; set; } = 6;

        /// <summary>
        /// hls_list_size
        /// </summary>
        public int winSize { get; set; } = 10;

        /// <summary>
        /// hls_segment_type fmp4 / mpegts
        /// </summary>
        public bool fmp4 { get; set; } = true;

        [JsonProperty("comand", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string[]> comand { get; set; } = new Dictionary<string, string[]>()
        {
            ["output"] = ["-max_delay 5000000"],
            ["segment_mpegts"] = ["-bsf:v h264_mp4toannexb"],
        };
    }

    public class TranscodingAudioOptions
    {
        [JsonIgnore]
        public int index { get; set; }

        public int bitrateKbps { get; set; } = 192;

        public bool stereo { get; set; } = true;

        [JsonProperty("codec_copy", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public string[] codec_copy { get; set; } = Array.Empty<string>();

        [JsonProperty("comand_transcode", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string[]> comand_transcode { get; set; } = new Dictionary<string, string[]>()
        {
            ["default"] = ["-c:a aac", "-ac {stereo}", "-b:a {bitrateKbps}", "-profile:a aac_low"]
        };
    }

    public class TranscodingPlaylistOptions
    {
        /// <summary>
        /// sped
        /// </summary>
        public double readrate { get; set; } = 1.6;

        /// <summary>
        /// 10 MB
        /// </summary>
        public int burst { get; set; } = 10485760;

        public bool delete_segments { get; set; } = true;
    }

    public class TranscodingConvertOptions
    {
        public bool transcodeVideo { get; set; }

        [JsonProperty("codec", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public string[] codec { get; set; } = { "mpeg4", "msmpeg4v3", "flv1", "av1" };

        [JsonProperty("comand", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string[]> comand { get; set; } = new Dictionary<string, string[]>()
        {
            ["default"] = ["-c:v libx264", "-preset veryfast", "-tune zerolatency", "-pix_fmt yuv420p"],
            
            ["h264_yuv420p10le"] = [
                "-vf", "scale=in_color_matrix=bt2020nc:out_color_matrix=bt709:in_range=pc:out_range=tv,format=yuv420p",
                "-c:v libx264", "-preset veryfast", "-tune zerolatency", "-pix_fmt yuv420p",
                "-x264-params", "colorprim=bt709:transfer=bt709:colormatrix=bt709",
                "-color_primaries bt709", "-color_trc bt709", "-colorspace bt709", "-color_range tv"
            ]
        };
    }

    public class TranscodingSubtitleOptions
    {
        [JsonProperty("codec", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public string[] codec { get; set; } = { "subrip", "webvtt", "ass", "ssa", "mov_text", "ttml", "sami" };

        [JsonProperty("comand", ObjectCreationHandling = ObjectCreationHandling.Replace, NullValueHandling = NullValueHandling.Ignore)]
        public string[] comand { get; set; } = ["-map 0:{subIndex}", "-an -vn", "-c:s webvtt", "-flush_packets 1", "-max_interleave_delta 0", "-muxpreload 0", "-muxdelay 0", "-f webvtt", "subs_{subIndex}.vtt"];
    }

    public record TranscodingStartContext(
        Uri Source,
        string UserAgent,
        string Referer,
        TranscodingHlsOptions HlsOptions,
        TranscodingAudioOptions Audio,
        bool live,
        bool subtitles,
        string OutputDirectory,
        int? startNumber,
        JObject ffprobe
    );
}
