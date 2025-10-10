using System;

namespace Shared.Models.AppConf
{
    public class TracksTranscodingConf
    {
        public bool enable { get; set; } = true;

        public string ffmpeg { get; set; }
            = string.Empty;

        public string tempRoot { get; set; }
            = "database/tracks/transcoding";

        public int idleTimeoutSec { get; set; } = 60;

        public int gracefulStopTimeoutMs { get; set; } = 1500;

        public int maxConcurrentJobs { get; set; } = 2;

        public string[] allowHosts { get; set; } = Array.Empty<string>();

        public string hmacKey { get; set; }
            = string.Empty;

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
