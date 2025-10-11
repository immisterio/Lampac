using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Shared;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Tracks.Engine;

namespace Tracks.Controllers
{
    [ApiController]
    [Route("transcoding")]
    public sealed class TranscodingController : Controller
    {
        private readonly TranscodingService _service = TranscodingService.Instance;

        #region Start
        [HttpGet("start.m3u8")]
        public IActionResult StartM3u8(string src)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (string.IsNullOrEmpty(src))
                return BadRequest(new { error = "src" });

            var (job, error) = _service.Start(new TranscodingStartRequest() { src = src });
            if (job == null)
                return BadRequest(new { error });

            return Redirect($"{AppInit.Host(HttpContext)}/transcoding/{job.StreamId}/index.m3u8");
        }

        [HttpPost("start")]
        public IActionResult Start([FromBody] TranscodingStartRequest request)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            var (job, error) = _service.Start(request);
            if (job == null)
                return BadRequest(new { error });

            return Ok(new
            {
                job.StreamId,
                playlistUrl = $"{AppInit.Host(HttpContext)}/transcoding/{job.StreamId}/index.m3u8",
                hls_timeout_seconds = 60
            });
        }
        #endregion

        #region Playlist
        [HttpGet("{streamId}/index.m3u8")]
        public async Task<IActionResult> Playlist(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            var path = job.Context.PlaylistPath;

            var fileExistsTimeout = TimeSpan.FromSeconds(60);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool fileExists = System.IO.File.Exists(path);

            while (!fileExists && sw.Elapsed < fileExistsTimeout)
            {
                await Task.Delay(250);
                fileExists = System.IO.File.Exists(path);
                if (fileExists)
                    break;
            }

            if (!fileExists)
                return NotFound();

            string m3u8 = null;

            sw.Restart();
            while (sw.Elapsed < fileExistsTimeout)
            {
                try
                {
                    m3u8 = System.IO.File.ReadAllText(path);
                }
                catch
                {
                    m3u8 = null;
                }

                if (!string.IsNullOrEmpty(m3u8) && Regex.IsMatch(m3u8, "seg_[0-9]+\\.(m4s|ts)"))
                    break;

                await Task.Delay(250);
            }

            if (string.IsNullOrEmpty(m3u8))
                return NotFound();

            m3u8 = Regex.Replace(m3u8, "#EXT-X-MAP:URI=[^\n\r]+", "#EXT-X-MAP:URI=\"init.mp4\"");

            return Content(m3u8, "application/vnd.apple.mpegurl");
        }
        #endregion

        #region Segment
        [HttpGet("{streamId}/{file}")]
        public IActionResult Segment(string streamId, string file)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            var resolved = _service.GetFilePath(job, file);
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
        #endregion

        #region Heartbeat
        [HttpPost("{streamId}/heartbeat")]
        public IActionResult Heartbeat(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);
            return Ok();
        }
        #endregion

        #region StopAsync
        [HttpPost("{streamId}/stop")]
        public async Task<IActionResult> StopAsync(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            var stopped = await _service.StopAsync(streamId);
            return stopped ? Ok() : NotFound();
        }
        #endregion

        #region Status
        [HttpGet("{streamId}/status")]
        public IActionResult Status(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            var now = DateTime.UtcNow;
            var idleTimeout = TimeSpan.FromSeconds(Math.Max(1, _service.IdleTimeoutSeconds));
            var state = job.Process.HasExited
                ? TranscodingJobState.Stopped
                : (now - job.LastAccessUtc) > idleTimeout
                    ? TranscodingJobState.Idle
                    : TranscodingJobState.Running;

            var uptime = now - job.StartedUtc;

            int? exitCode = null;
            try
            {
                if (job.Process.HasExited)
                    exitCode = job.Process.ExitCode;
            }
            catch
            {
            }

            return Ok(new
            {
                job.StreamId,
                state = state.ToString(),
                startedUtc = job.StartedUtc,
                lastAccessUtc = job.LastAccessUtc,
                uptime = uptime.TotalSeconds,
                bitrateKbps = job.LastBitrateKbps,
                ffmpeg = _service.FfmpegPath,
                exitCode,
                log = job.SnapshotLog()
            });
        }
        #endregion
    }
}
