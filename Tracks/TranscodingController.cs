using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Shared;
using System;
using System.Threading.Tasks;
using Tracks.Engine;

namespace Tracks.Controllers
{
    [ApiController]
    [Route("transcoding")]
    public sealed class TranscodingController : Controller
    {
        private readonly TranscodingService _service = TranscodingService.Instance;

        [HttpPost("start")]
        public async Task<IActionResult> StartAsync([FromBody] TranscodingStartRequest request)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            var (job, error) = await _service.StartAsync(request);
            if (job == null)
            {
                if (string.Equals(error, "Maximum concurrent jobs reached", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(StatusCodes.Status429TooManyRequests, new { error });

                if (string.Equals(error, "Transcoding disabled", StringComparison.OrdinalIgnoreCase))
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error });

                return BadRequest(new { error });
            }

            return Ok(new
            {
                job.StreamId,
                playlistUrl = $"/transcoding/{job.StreamId}/index.m3u8"
            });
        }

        [HttpGet("{streamId}/index.m3u8")]
        public IActionResult Playlist(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            var path = job.Context.PlaylistPath;
            if (!System.IO.File.Exists(path))
                return NotFound();

            return PhysicalFile(path, "application/vnd.apple.mpegurl");
        }

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

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(resolved, out var contentType))
                contentType = "application/octet-stream";

            return PhysicalFile(resolved, contentType, enableRangeProcessing: true);
        }

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

        [HttpPost("{streamId}/stop")]
        public async Task<IActionResult> StopAsync(string streamId)
        {
            if (!AppInit.conf.trackstranscoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Initialization false" });

            var stopped = await _service.StopAsync(streamId);
            return stopped ? Ok() : NotFound();
        }

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
    }
}
