using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.WebUtilities;
using Shared;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tracks.Engine;

namespace Tracks.Controllers
{
    [ApiController]
    [Route("videos/{streamId}")]
    public sealed class VideoController : Controller
    {
        private static readonly Regex SegmentFileRegex = new(@"seg_(\d+)\.(?:m4s|ts)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ExtInfRegex = new("#EXTINF:([^,]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PlaylistState> PlaylistStates = new(StringComparer.OrdinalIgnoreCase);

        private readonly TranscodingService _service = TranscodingService.Instance;

        [HttpGet("main.m3u8")]
        public async Task<IActionResult> Playlist(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            var path = job.Context.PlaylistPath;
            var fileExistsTimeout = TimeSpan.FromSeconds(60);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            var fileExists = System.IO.File.Exists(path);
            while (!fileExists && sw.Elapsed < fileExistsTimeout)
            {
                await Task.Delay(250);
                fileExists = System.IO.File.Exists(path);
                if (fileExists)
                    break;
            }

            if (!fileExists)
                return NotFound();

            string sourcePlaylist = null;

            sw.Restart();
            while (sw.Elapsed < fileExistsTimeout)
            {
                try
                {
                    sourcePlaylist = System.IO.File.ReadAllText(path);
                }
                catch
                {
                    sourcePlaylist = null;
                }

                if (!string.IsNullOrEmpty(sourcePlaylist) && SegmentFileRegex.IsMatch(sourcePlaylist))
                    break;

                await Task.Delay(250);
            }

            if (string.IsNullOrEmpty(sourcePlaylist))
                return NotFound();

            var baseUrl = $"{AppInit.Host(HttpContext)}/videos/{streamId}/hls1/main";
            var baseQuery = HttpContext.Request.QueryString.HasValue
                ? HttpContext.Request.QueryString.Value.TrimStart('?')
                : string.Empty;

            var state = PlaylistStates.GetOrAdd(job.StreamId, _ => new PlaylistState(job.StartedUtc, job.Context.HlsOptions.segDur));

            string playlist;

            lock (state.SyncRoot)
            {
                if (state.StartedUtc != job.StartedUtc)
                    state.Reset(job.StartedUtc, job.Context.HlsOptions.segDur);

                state.MapUrl = BuildSegmentUrl($"{baseUrl}/-1.mp4", baseQuery, 0, 0);
                state.DefaultDuration = job.Context.HlsOptions.segDur;

                UpdateStateFromSource(state, sourcePlaylist);

                playlist = BuildVodPlaylist(state, baseUrl, baseQuery);
            }

            return Content(playlist, "application/vnd.apple.mpegurl");
        }

        [HttpGet("hls1/main/{segment}.mp4")]
        public IActionResult Segment(string streamId, string segment)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            string fileName;
            if (segment == "-1")
            {
                fileName = "init.mp4";
            }
            else if (int.TryParse(segment, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segmentIndex) && segmentIndex >= 0)
            {
                var ext = job.Context.HlsOptions.fmp4 ? ".m4s" : ".ts";
                fileName = $"seg_{segmentIndex:D5}{ext}";
            }
            else
            {
                return NotFound();
            }

            var resolved = _service.GetFilePath(job, fileName);
            if (resolved == null)
                return NotFound();

            var provider = new FileExtensionContentTypeProvider()
            {
                Mappings =
                {
                    [".m4s"]  = "video/mp4",
                    [".ts"]   = "video/mp2t",
                    [".mp4"]  = "video/mp4",
                    [".m3u"]  = "application/x-mpegURL",
                    [".m3u8"] = "application/vnd.apple.mpegurl",
                    [".m2ts"] = "video/MP2T"
                }
            };

            if (!provider.TryGetContentType(resolved, out var contentType))
                contentType = "application/octet-stream";

            return File(System.IO.File.OpenRead(resolved), contentType, enableRangeProcessing: true);
        }

        private static double ParseDuration(string extinfLine, double fallback)
        {
            var match = ExtInfRegex.Match(extinfLine);
            if (!match.Success)
                return fallback;

            if (double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
                return duration;

            return fallback;
        }

        private static void UpdateStateFromSource(PlaylistState state, string source)
        {
            using var reader = new StringReader(source);

            string line;
            double lastDuration = state.DefaultDuration;
            double? pendingDuration = null;
            var pendingDirectives = new List<string>();
            var segmentsSeen = false;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#EXT-X-MAP", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                {
                    var durationValue = ParseDuration(line, lastDuration);
                    pendingDuration = durationValue;
                    lastDuration = durationValue;
                    continue;
                }

                if (line.StartsWith("#", StringComparison.Ordinal))
                {
                    var directiveKey = GetDirectiveKey(line);

                    if (string.Equals(directiveKey, "#EXTM3U", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(directiveKey))
                        continue;

                    if (string.Equals(directiveKey, "#EXT-X-VERSION", StringComparison.OrdinalIgnoreCase))
                    {
                        state.VersionLine = line;
                        continue;
                    }

                    if (string.Equals(directiveKey, "#EXT-X-PLAYLIST-TYPE", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(directiveKey, "#EXT-X-TARGETDURATION", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(directiveKey, "#EXT-X-MEDIA-SEQUENCE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.Equals(directiveKey, "#EXT-X-ENDLIST", StringComparison.OrdinalIgnoreCase))
                    {
                        state.HasEndList = true;
                        continue;
                    }

                    if (!segmentsSeen)
                        state.SetPlaylistDirective(directiveKey, line);
                    else
                        pendingDirectives.Add(line);

                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = SegmentFileRegex.Match(line);
                if (!match.Success)
                    continue;

                var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var duration = pendingDuration ?? lastDuration;
                if (duration <= 0)
                    duration = state.DefaultDuration;

                state.AddOrUpdateSegment(index, duration, pendingDirectives);

                pendingDirectives.Clear();
                pendingDuration = null;
                lastDuration = duration;
                segmentsSeen = true;
            }
        }

        private static string BuildVodPlaylist(PlaylistState state, string baseUrl, string baseQuery)
        {
            var sb = new StringBuilder();
            sb.AppendLine("#EXTM3U");
            sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");

            var versionLine = state.VersionLine;
            if (string.IsNullOrWhiteSpace(versionLine))
                versionLine = "#EXT-X-VERSION:7";
            sb.AppendLine(versionLine);

            var targetDuration = (int)Math.Max(1, Math.Ceiling(state.MaxDuration > 0 ? state.MaxDuration : state.DefaultDuration));
            sb.AppendLine($"#EXT-X-TARGETDURATION:{targetDuration}");
            sb.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

            foreach (var key in state.PlaylistDirectiveOrder)
            {
                if (state.PlaylistDirectives.TryGetValue(key, out var directive))
                    sb.AppendLine(directive);
            }

            if (!string.IsNullOrEmpty(state.MapUrl))
                sb.AppendLine($"#EXT-X-MAP:URI=\"{state.MapUrl}\"");

            long runtimeTicks = 0;

            foreach (var segment in state.Segments)
            {
                foreach (var directive in segment.Directives)
                    sb.AppendLine(directive);

                var duration = segment.Duration > 0 ? segment.Duration : state.DefaultDuration;
                var extinf = $"#EXTINF:{duration.ToString("0.000000", CultureInfo.InvariantCulture)}, nodesc";
                sb.AppendLine(extinf);

                var actualTicks = Math.Max(1L, (long)Math.Round(duration * TimeSpan.TicksPerSecond));
                var segmentUrl = BuildSegmentUrl($"{baseUrl}/{segment.Index}.mp4", baseQuery, runtimeTicks, actualTicks);
                sb.AppendLine(segmentUrl);

                runtimeTicks += actualTicks;
            }

            if (state.HasEndList)
                sb.AppendLine("#EXT-X-ENDLIST");

            return sb.ToString();
        }

        private static string GetDirectiveKey(string line)
        {
            if (string.IsNullOrEmpty(line))
                return line;

            var idx = line.IndexOf(':');
            if (idx < 0)
                idx = line.IndexOf('=');

            return idx > 0 ? line[..idx] : line;
        }

        private static string BuildSegmentUrl(string basePath, string baseQuery, long runtimeTicks, long actualTicks)
        {
            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(baseQuery))
            {
                foreach (var pair in QueryHelpers.ParseQuery("?" + baseQuery))
                {
                    if (pair.Value.Count > 0)
                        parameters[pair.Key] = pair.Value.Last();
                    else
                        parameters[pair.Key] = string.Empty;
                }
            }

            parameters["runtimeTicks"] = runtimeTicks.ToString(CultureInfo.InvariantCulture);
            parameters["actualSegmentLengthTicks"] = actualTicks.ToString(CultureInfo.InvariantCulture);

            return QueryHelpers.AddQueryString(basePath, parameters);
        }

        private sealed class PlaylistState
        {
            private readonly Dictionary<int, SegmentInfo> _segmentLookup = new();

            public PlaylistState(DateTime startedUtc, double defaultDuration)
            {
                StartedUtc = startedUtc;
                DefaultDuration = defaultDuration;
            }

            public object SyncRoot { get; } = new();

            public DateTime StartedUtc { get; private set; }

            public double DefaultDuration { get; set; }

            public string VersionLine { get; set; }

            public string MapUrl { get; set; }

            public bool HasEndList { get; set; }

            public double MaxDuration { get; private set; }

            public List<SegmentInfo> Segments { get; } = new();

            public List<string> PlaylistDirectiveOrder { get; } = new();

            public Dictionary<string, string> PlaylistDirectives { get; } = new(StringComparer.OrdinalIgnoreCase);

            public void Reset(DateTime startedUtc, double defaultDuration)
            {
                StartedUtc = startedUtc;
                DefaultDuration = defaultDuration;
                VersionLine = null;
                MapUrl = null;
                HasEndList = false;
                MaxDuration = 0;
                Segments.Clear();
                PlaylistDirectiveOrder.Clear();
                PlaylistDirectives.Clear();
                _segmentLookup.Clear();
            }

            public void SetPlaylistDirective(string key, string value)
            {
                if (!PlaylistDirectives.ContainsKey(key))
                    PlaylistDirectiveOrder.Add(key);

                PlaylistDirectives[key] = value;
            }

            public void AddOrUpdateSegment(int index, double duration, List<string> directives)
            {
                if (_segmentLookup.TryGetValue(index, out var existing))
                {
                    existing.Duration = duration;
                    existing.SetDirectives(directives);
                }
                else
                {
                    var info = new SegmentInfo(index, duration);
                    info.SetDirectives(directives);
                    InsertSegment(info);
                    _segmentLookup[index] = info;
                }

                if (duration > MaxDuration)
                    MaxDuration = duration;
            }

            private void InsertSegment(SegmentInfo info)
            {
                if (Segments.Count == 0)
                {
                    Segments.Add(info);
                    return;
                }

                var index = Segments.BinarySearch(info, SegmentInfoComparer.Instance);
                if (index < 0)
                    index = ~index;

                if (index >= Segments.Count)
                    Segments.Add(info);
                else
                    Segments.Insert(index, info);
            }
        }

        private sealed class SegmentInfo
        {
            public SegmentInfo(int index, double duration)
            {
                Index = index;
                Duration = duration;
            }

            public int Index { get; }

            public double Duration { get; set; }

            public List<string> Directives { get; private set; } = new();

            public void SetDirectives(List<string> directives)
            {
                Directives = directives.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
            }
        }

        private sealed class SegmentInfoComparer : IComparer<SegmentInfo>
        {
            public static readonly SegmentInfoComparer Instance = new();

            public int Compare(SegmentInfo x, SegmentInfo y)
            {
                if (ReferenceEquals(x, y))
                    return 0;

                if (x is null)
                    return -1;

                if (y is null)
                    return 1;

                return x.Index.CompareTo(y.Index);
            }
        }
    }
}
