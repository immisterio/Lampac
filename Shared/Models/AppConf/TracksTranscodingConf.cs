namespace Shared.Models.AppConf
{
    public class TracksTranscodingConf
    {
        public bool enable { get; set; }

        public string ffmpeg { get; set; }

        public string tempRoot { get; set; }

        public int idleTimeoutSec { get; set; }

        public int maxConcurrentJobs { get; set; }

        public string[] allowHosts { get; set; } = Array.Empty<string>();

        public TranscodingHlsOptions hlsOptions { get; set; } = new();

        public TranscodingAudioOptions audioOptions { get; set; } = new();
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
}
