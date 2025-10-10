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
    }

    public sealed class TranscodingAudioOptions
    {
        public bool transcodeToAac { get; set; } = true;

        public int bitrateKbps { get; set; } = 190;

        public bool stereo { get; set; } = true;
    }

    public sealed class TranscodingHlsOptions
    {
        public int segDur { get; set; } = 6;

        public int winSize { get; set; } = 12;

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
        bool UseFmp4,
        int SegmentDuration,
        int PlaylistSize,
        TranscodingAudioOptions Audio,
        bool HasAudioStream,
        TranscodeMode Mode,
        string SegmentTemplate,
        string PlaylistPath,
        string OutputDirectory
    );

    internal enum TranscodeMode
    {
        DirectRemux,
        FullTranscode
    }
}
