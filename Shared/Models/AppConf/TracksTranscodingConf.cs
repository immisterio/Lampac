namespace Shared.Models.AppConf
{
    public class TracksTranscodingConf
    {
        public bool enable { get; set; }

        public string ffmpeg { get; set; }

        public string tempRoot { get; set; }

        public int idleTimeoutSec { get; set; }

        public int gracefulStopTimeoutMs { get; set; }

        public int maxConcurrentJobs { get; set; }

        public string[] allowHosts { get; set; } = Array.Empty<string>();

        public string hmacKey { get; set; }

        public TracksTranscodingHls defaults { get; set; } = new();

        public int janitorSweepSec { get; set; } = 5;
    }

    public class TracksTranscodingHls
    {
        public int segDur { get; set; } = 6;

        public int winSize { get; set; } = 12;

        public bool fmp4 { get; set; } = true;
    }
}
