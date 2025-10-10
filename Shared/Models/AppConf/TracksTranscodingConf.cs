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

        public string hmacKey { get; set; }

        public TranscodingHlsOptions hlsOptions { get; set; } = new();

        public TranscodingAudioOptions audioOptions { get; set; } = new();

        public int janitorSweepSec { get; set; } = 5;
    }

    public class TranscodingHlsOptions
    {
        /// <summary>
        /// hls_time
        /// </summary>
        public int segDur { get; set; } = 3;

        /// <summary>
        /// hls_list_size
        /// </summary>
        public int winSize { get; set; } = 20;

        /// <summary>
        /// hls_segment_type fmp4 / mpegts
        /// </summary>
        public bool fmp4 { get; set; } = true;
    }

    public class TranscodingAudioOptions
    {
        public bool transcodeToAac { get; set; } = true;

        public int bitrateKbps { get; set; } = 192;

        public bool stereo { get; set; } = true;
    }
}
