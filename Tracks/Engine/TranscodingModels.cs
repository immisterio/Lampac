using System;
using System.Collections.Generic;

namespace Tracks.Engine
{
    public sealed class TranscodingStartRequest
    {
        public string src { get; set; } = string.Empty;

        public TranscodingAudioOptions audio { get; set; } = new();

        public TranscodingHlsOptions hls { get; set; } = new();

        public Dictionary<string, string> headers { get; set; } = new();

        public bool subtitles { get; set; }
    }

    public sealed class TranscodingAudioOptions
    {
        public bool transcodeToAac { get; set; } = true;

        public int bitrateKbps { get; set; } = 192;

        public bool stereo { get; set; } = true;
    }

    public sealed class TranscodingHlsOptions
    {
        /// <summary>
        /// hls_time
        /// </summary>
        public int segDur { get; set; } = 6;

        /// <summary>
        /// hls_list_size
        /// </summary>
        public int winSize { get; set; } = 12;

        /// <summary>
        /// hls_segment_type fmp4 / mpegts
        /// </summary>
        public bool fmp4 { get; set; } = true;
    }

    public enum TranscodingJobState
    {
        Running,
        Idle,
        Stopped
    }

    internal sealed record TranscodingStartContext(
        Uri Source,
        string UserAgent,
        string Referer,
        TranscodingHlsOptions HlsOptions,
        TranscodingAudioOptions Audio,
        bool subtitles,
        string OutputDirectory,
        string PlaylistPath
    );
}
