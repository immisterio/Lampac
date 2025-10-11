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
        private static readonly Regex SegmentFileRegex = new("seg_[0-9]+\\.(m4s|ts)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex ExtInfRegex = new("#EXTINF:([^,]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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

            var sb = new StringBuilder();
            using var reader = new StringReader(sourcePlaylist);

            string line;
            long runtimeTicks = 0;
            int segmentIndex = 0;
            double lastDuration = job.Context.HlsOptions.segDur;

            var mapUrl = BuildSegmentUrl($"{baseUrl}/-1.mp4", baseQuery, 0, 0);

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#EXT-X-MAP", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine($"#EXT-X-MAP:URI=\"{mapUrl}\"");
                    continue;
                }

                if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                {
                    var duration = ParseDuration(line, lastDuration);
                    lastDuration = duration;
                    sb.AppendLine($"#EXTINF:{duration.ToString("0.000000", CultureInfo.InvariantCulture)}, nodesc");
                    continue;
                }

                if (line.StartsWith("#"))
                {
                    sb.AppendLine(line);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var actualTicks = Math.Max(1L, (long)Math.Round(lastDuration * TimeSpan.TicksPerSecond));
                var segmentUrl = BuildSegmentUrl($"{baseUrl}/{segmentIndex}.mp4", baseQuery, runtimeTicks, actualTicks);

                sb.AppendLine(segmentUrl);

                runtimeTicks += actualTicks;
                segmentIndex++;
            }

            return Content(sb.ToString(), "application/vnd.apple.mpegurl");
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
    }
}
