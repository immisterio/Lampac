using Shared.Models.AppConf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Tracks.Engine
{
    internal sealed class TranscodingJob : IDisposable
    {
        private const int MaxLogLines = 200;

        private readonly LinkedList<string> _log = new();
        private readonly object _logSync = new();
        private readonly CancellationTokenSource _cts = new();

        public TranscodingJob(string id, string streamId, string outputDirectory, Process process, TranscodingStartContext context)
        {
            Id = id;
            StreamId = streamId;
            OutputDirectory = outputDirectory;
            Process = process;
            Context = context;
            StartedUtc = DateTime.UtcNow;
            LastAccessUtc = StartedUtc;
        }

        public string Id { get; }

        public string StreamId { get; }

        public string OutputDirectory { get; }

        public Process Process { get; }

        public TranscodingStartContext Context { get; }

        public DateTime StartedUtc { get; }

        public DateTime LastAccessUtc { get; private set; }

        public int? ExitCode { get; private set; }

        public bool HasExited => Process.HasExited;

        public CancellationToken CancellationToken => _cts.Token;

        public void UpdateLastAccess() => LastAccessUtc = DateTime.UtcNow;

        public int duration { get; private set; }

        public string videoFormat { get; private set; }


        public void AppendLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            lock (_logSync)
            {
                foreach (var part in line.Split('\n'))
                {
                    string trimmed = part.TrimEnd('\r');
                    if (string.IsNullOrWhiteSpace(trimmed))
                        continue;

                    #region duration
                    string _duration = Regex.Match(trimmed, "Duration:[\t ]+([0-9\\:\\.]+)", RegexOptions.IgnoreCase).Groups[1].Value;
                    if (!string.IsNullOrEmpty(_duration))
                    {
                        if (TimeSpan.TryParseExact(_duration, @"hh\:mm\:ss\.ff", null, out var timeSpan))
                            duration = (int)timeSpan.TotalSeconds;
                    }
                    #endregion

                    #region video
                    string _video = Regex.Match(trimmed, "Video:[\t ]+([^\t ]+)", RegexOptions.IgnoreCase).Groups[1].Value;
                    if (videoFormat == null && !string.IsNullOrEmpty(_video))
                        videoFormat = _video.Trim();
                    #endregion

                    if (trimmed.Length > 2000)
                        trimmed = trimmed[..2000];

                    _log.AddLast(trimmed);
                    if (_log.Count > MaxLogLines)
                        _log.RemoveFirst();
                }
            }
        }

        public string[] SnapshotLog()
        {
            lock (_logSync)
                return _log.ToArray();
        }

        public void SignalExit()
        {
            if (Process.HasExited)
                ExitCode = Process.ExitCode;
        }

        public void StopBackground()
            => _cts.Cancel();

        public void Dispose()
        {
            try
            {
                StopBackground();
            }
            catch { }

            try
            {
                Process.Dispose();
            }
            catch { }
        }
    }
}
