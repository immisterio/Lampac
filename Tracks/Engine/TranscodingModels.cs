using Shared.Models.AppConf;
using System;
using System.Collections.Generic;

namespace Tracks.Engine
{
    public sealed class TranscodingStartRequest
    {
        public string src { get; set; } = string.Empty;

        public TranscodingAudioOptions audio { get; set; }

        public TranscodingHlsOptions hls { get; set; }

        public Dictionary<string, string> headers { get; set; }

        public bool live { get; set; }

        public bool subtitles { get; set; }
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
        bool live,
        bool subtitles,
        string OutputDirectory,
        string PlaylistPath
    );
}
