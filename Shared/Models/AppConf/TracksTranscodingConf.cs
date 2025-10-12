namespace Shared.Models.AppConf
{
    public class TracksTranscodingConf
    {
        public bool enable { get; set; }

        public string ffmpeg { get; set; }

        public string tempRoot { get; set; }

        public int idleTimeoutSec { get; set; }

        public int idleTimeoutSec_live { get; set; }

        public int maxConcurrentJobs { get; set; }

        public string[] allowHosts { get; set; } = Array.Empty<string>();

        public TranscodingHlsOptions hlsOptions { get; set; } = new();

        public TranscodingAudioOptions audioOptions { get; set; } = new();

        public TranscodingPlaylistOptions playlistOptions { get; set; } = new();

        public TranscodingVideoOptions videoOptions { get; set; } = new();
    }

    public class TranscodingHlsOptions
    {
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
    }

    public class TranscodingAudioOptions
    {
        public int index { get; set; }

        public bool transcodeToAac { get; set; } = true;

        public int bitrateKbps { get; set; } = 192;

        public bool stereo { get; set; } = true;
    }

    public class TranscodingPlaylistOptions
    {
        public double readrate { get; set; } = 1.1;

        public int burstSec { get; set; } = 180;

        public bool delete_segments { get; set; } = true;
    }

    public class TranscodingVideoOptions
    {
        public string[] formats { get; set; } = { "avi", "flv", "h265", "av1" };

        public string[] args { get; set; } = { "libx264", "-preset veryfast", "-tune zerolatency", "-pix_fmt yuv420p" };
    }

    public record TranscodingStartContext(
        Uri Source,
        string UserAgent,
        string Referer,
        TranscodingHlsOptions HlsOptions,
        TranscodingAudioOptions Audio,
        bool live,
        int? subtitles,
        string OutputDirectory,
        string PlaylistPath,
        int? startNumber,
        string videoFormat
    );
}
