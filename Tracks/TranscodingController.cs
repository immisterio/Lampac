using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using Shared;
using Shared.Engine;
using Shared.Models.AppConf;
using Shared.Models.Templates;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Tracks.Engine;

namespace Tracks.Controllers
{
    [ApiController]
    [Route("transcoding")]
    public sealed class TranscodingController : Controller
    {
        #region static
        readonly TranscodingService _service = TranscodingService.Instance;

        static readonly FileExtensionContentTypeProvider provider = new FileExtensionContentTypeProvider()
        {
            Mappings = {
                [".m4s"] = "video/mp4",
                [".ts"] = "video/mp2t",
                [".mp4"] = "video/mp4",
                [".m2ts"] = "video/MP2T"
            }
        };
        #endregion

        #region transcoding.js
        [AllowAnonymous]
        [HttpGet("/transcoding.js")]
        [HttpGet("js/{token}")]
        public ActionResult TranscodingJs(string token)
        {
            if (!AppInit.conf.transcoding.enable)
                return Content(string.Empty);

            var sb = new StringBuilder(FileCache.ReadAllText("plugins/transcoding.js"));

            sb.Replace("{localhost}", AppInit.Host(HttpContext))
              .Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
        }
        #endregion


        #region Start
        [HttpGet("start.m3u8")]
        public async Task<IActionResult> StartM3u8(string src, int a, int s, bool subtitles, bool live)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (string.IsNullOrEmpty(src))
                return BadRequest(new { error = "src" });

            if (src.Contains("/proxy/") && src.Contains(".mkv"))
            {
                string hash = Regex.Match(src, "/proxy/([^\n\r]+\\.mkv)").Groups[1].Value;
                src = ProxyLink.Decrypt(hash, null)?.uri;
                if (string.IsNullOrWhiteSpace(src))
                    return BadRequest(new { error = "src decrypt" });
            }

            var defaults = AppInit.conf.transcoding;

            var (job, error) = await _service.Start(new TranscodingStartRequest() 
            { 
                src = src,
                live = live,
                subtitles = subtitles,
                audio = new TranscodingAudioOptions() { index = a },
                hls = new TranscodingHlsOptions() { seek = s }
            });

            if (job == null)
                return BadRequest(new { error });

            string uri = $"{AppInit.Host(HttpContext)}/transcoding/{job.StreamId}/{(live ? "live" : "main")}.m3u8";

            return Redirect(AccsDbInvk.Args(uri, HttpContext));
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] TranscodingStartRequest request)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (request == null)
                return BadRequest(new { error = "Request body is required" });

            var (job, error) = await _service.Start(request);
            if (job == null)
                return BadRequest(new { error });

            return Ok(new
            {
                job.StreamId,
                playlistUrl = AccsDbInvk.Args($"{AppInit.Host(HttpContext)}/transcoding/{job.StreamId}/{(job.Context.live ? "live" : "main")}.m3u8", HttpContext),
                subtitlesUrl = job.Context.subtitles ? AccsDbInvk.Args($"{AppInit.Host(HttpContext)}/transcoding/{job.StreamId}/subtitles", HttpContext) : null,
                hls_timeout_seconds = 60
            });
        }
        #endregion

        #region Seek
        [HttpGet("{streamId}/seek/{ss}")]
        public async Task<IActionResult> Seek(string streamId, int ss)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            if (!job.Context.live)
                return BadRequest(new { error = "Context not live" });

            if (ss < 0)
                return BadRequest(new { error = "ss must be greater or equal 0" });

            var (success, error) = await _service.SeekAsync(streamId, ss);
            if (!success)
                return BadRequest(new { error });

            return Ok();
        }
        #endregion

        #region Live
        [HttpGet("{streamId}/live.m3u8")]
        public async Task<IActionResult> Live(string streamId)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            if (!job.Context.live)
                return BadRequest(new { error = "Context not live" });

            _service.Touch(job);

            string path = Path.Combine(job.Context.OutputDirectory, "index.m3u8");

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

            m3u8 = Regex.Replace(m3u8, "#EXT-X-MAP:URI=[^\n\r]+", $"#EXT-X-MAP:URI=\"{AccsDbInvk.Args("init.mp4", HttpContext)}\"");
            m3u8 = Regex.Replace(m3u8, "(seg_[0-9]+\\.(m4s|ts))", r =>
            {
                string file = r.Groups[1].Value;
                return AccsDbInvk.Args(file, HttpContext);
            });

            return Content(m3u8, "application/vnd.apple.mpegurl");
        }
        #endregion

        #region Playlist
        [HttpGet("{streamId}/main.m3u8")]
        public async Task<IActionResult> Playlist(string streamId)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            if (job.Context.live)
                return BadRequest(new { error = "Context not playlist" });

            if (!int.TryParse(job.Context.ffprobe["format"].Value<string>("duration").Split('.')[0].Split(',')[0], out int duration) || duration == 0)
                return BadRequest(new { error = "duration" });

            _service.Touch(job);

            var fileExistsTimeout = TimeSpan.FromSeconds(60);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            while (sw.Elapsed < fileExistsTimeout)
            {
                if (Directory.GetFiles(job.Context.OutputDirectory).Length > 2)
                    break;

                await Task.Delay(250);
            }

            int segDur = job.Context.HlsOptions.segDur;

            var builder = new StringBuilder();
            builder.AppendLine("#EXTM3U");
            builder.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
            builder.AppendLine($"#EXT-X-VERSION:{(job.Context.HlsOptions.fmp4 ? 7 : 3)}");
            builder.AppendLine($"#EXT-X-TARGETDURATION:{segDur}");
            builder.AppendLine("#EXT-X-MEDIA-SEQUENCE:0");

            if (job.Context.HlsOptions.fmp4)
                builder.AppendLine($"#EXT-X-MAP:URI=\"{AccsDbInvk.Args("init.mp4", HttpContext)}\"");

            for (int i = 0; i < (duration / segDur); i++)
            {
                builder.AppendLine($"#EXTINF:{segDur}.0,");
                builder.AppendLine(AccsDbInvk.Args($"seg_{i:d5}.{(job.Context.HlsOptions.fmp4 ? "m4s" : "ts")}", HttpContext));
            }

            builder.AppendLine("#EXT-X-ENDLIST");

            return Content(builder.ToString(), "application/vnd.apple.mpegurl");
        }
        #endregion

        #region Segment
        [HttpGet("{streamId}/{file}")]
        public async Task<IActionResult> Segment(string streamId, string file)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            int? segmentIndex = null;
            if (!job.Context.live && file != null)
            {
                var matchSegment = Regex.Match(file, @"seg_(\d+)\.(m4s|ts)$", RegexOptions.IgnoreCase);
                if (matchSegment.Success && int.TryParse(matchSegment.Groups[1].Value, out var idx))
                    segmentIndex = idx;
            }

            var fileExistsTimeout = TimeSpan.FromSeconds(60);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            string resolved = _service.GetFilePath(job, file);

            if (job.Context.live == false && resolved == null && !file.Contains(".vtt"))
            {
                #region SeekAsync
                if (segmentIndex.HasValue)
                {
                    int segDur = job.Context.HlsOptions.segDur;
                    int ss = segmentIndex.Value * segDur;

                    if (job.Context.HlsOptions.seek == 0 && 30 > ss)
                    {
                        // первые 30 секунд без seek-а - не трогаем
                    }
                    else if (job.Context.HlsOptions.seek == ss)
                    {
                        // ffmpeg на текущем сегменте
                    }
                    else
                    {
                        // работает дальше чем текущий сегмент
                        bool goSeek = job.Context.HlsOptions.seek > ss;

                        string extension = Path.GetExtension(file);
                        int segmentsPerMinute = (int)Math.Ceiling(30.0 / segDur);
                        int startIndex = Math.Max(0, segmentIndex.Value - segmentsPerMinute);

                        if (goSeek == false)
                        {
                            goSeek = true;

                            for (int i = startIndex; i < segmentIndex.Value; i++)
                            {
                                string candidate = $"seg_{i:d5}{extension}";
                                if (_service.GetFilePath(job, candidate) != null)
                                {
                                    // есть сегменты в пределах последних 30 секунд
                                    goSeek = false;
                                    break;
                                }
                            }
                        }

                        if (goSeek)
                            await _service.SeekAsync(streamId, ss, segmentIndex.Value);
                    }
                }
                #endregion

                do
                {
                    try
                    {
                        await Task.Delay(200);
                        resolved = _service.GetFilePath(job, file);
                        if (resolved != null)
                            break;
                    }
                    catch { }
                }
                while (sw.Elapsed < fileExistsTimeout);
            }

            if (resolved == null)
                return NotFound();

            #region ждем появления следующего сегмента
            while (segmentIndex.HasValue && sw.Elapsed < fileExistsTimeout)
            {
                try
                {
                    string seg = Path.Combine(job.OutputDirectory, $"seg_{segmentIndex.Value + 1:d5}");
                    if (System.IO.File.Exists($"{seg}.{(job.Context.HlsOptions.fmp4 ? "m4s" : "ts")}"))
                        break;
                }
                catch { }

                await Task.Delay(200);
            }
            #endregion

            #region FileStream
            FileStream fs = null;
            do
            {
                try
                {
                    fs = file.Contains(".vtt")
                        ? new FileStream(resolved, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        : System.IO.File.OpenRead(resolved);
                    break;
                }
                catch (IOException)
                {
                    await Task.Delay(200);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(200);
                }
            } 
            while (sw.Elapsed < fileExistsTimeout);

            if (fs == null)
                return NotFound();
            #endregion

            if (!provider.TryGetContentType(resolved, out var contentType))
                contentType = "application/octet-stream";

            if (segmentIndex.HasValue)
                _service.ReportSegmentAccess(job, segmentIndex.Value);

            return File(fs, contentType, enableRangeProcessing: true);
        }
        #endregion

        #region Subtitles
        [HttpGet("{streamId}/subtitles")]
        public IActionResult Subtitles(string streamId)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);

            var subsTpl = new SubtitleTpl();

            if (job.Context.subtitles && job.Context.ffprobe.ContainsKey("streams"))
            {
                foreach (var s in job.Context.ffprobe["streams"])
                {
                    if (s.Value<string>("codec_type") != "subtitle")
                        continue;

                    if (s.Value<string>("codec_name") is "subrip" or "webvtt" or "ass" or "ssa" or "mov_text" or "ttml" or "sami")
                    {
                        int subIndex = s.Value<int>("index");
                        if (subIndex == 0)
                            continue;

                        string uri = $"{AppInit.Host(HttpContext)}/transcoding/{streamId}/{AccsDbInvk.Args($"subs_{subIndex}.vtt", HttpContext)}";

                        string name = s["tags"].Value<string>("title")
                            ?? s["language"].Value<string>("title")
                            ?? $"sub_{subIndex}";

                        subsTpl.Append(name, uri);
                    }
                }
            }

            return Ok(subsTpl.ToObject());
        }
        #endregion

        #region Heartbeat
        [HttpGet("{streamId}/heartbeat")]
        public IActionResult Heartbeat(string streamId)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            _service.Touch(job);
            return Ok();
        }
        #endregion

        #region StopAsync
        [HttpGet("{streamId}/stop")]
        public async Task<IActionResult> StopAsync(string streamId)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            var stopped = await _service.StopAsync(streamId);
            return stopped ? Ok() : NotFound();
        }
        #endregion

        #region Status
        [HttpGet("{streamId}/status")]
        public IActionResult Status(string streamId)
        {
            if (!AppInit.conf.transcoding.enable || !ModInit.IsInitialization)
                return BadRequest(new { error = "Transcoding disabled" });

            if (!_service.TryResolveJob(streamId, out var job))
                return NotFound();

            var now = DateTime.UtcNow;
            var state = job.Process.HasExited ? TranscodingJobState.Stopped : TranscodingJobState.Running;

            var uptime = now - job.StartedUtc;

            int? exitCode = null;
            try
            {
                if (job.Process.HasExited)
                    exitCode = job.Process.ExitCode;
            }
            catch { }

            var snapshotLog = job.SnapshotLog();

            ulong time_ms = 0;
            foreach (string line in snapshotLog.Reverse())
            {
                if (!line.Contains("out_time_ms="))
                    continue;

                if (!ulong.TryParse(Regex.Match(line, "out_time_ms=([0-9]+)").Groups[1].Value, out time_ms))
                    continue;

                break;
            }

            return Content(JsonConvert.SerializeObject(new
            {
                job.StreamId,
                state = state.ToString(),
                job.OutputDirectory,
                ffmpeg = string.Join(" ", job.Process.StartInfo.ArgumentList),
                startedUtc = job.StartedUtc,
                lastAccessUtc = job.LastAccessUtc,
                uptime = uptime.TotalSeconds,
                positionSec = (ulong)(job.Context.HlsOptions.seek + (time_ms > 0 ? (time_ms / 1000000.0) : 0)),
                exitCode,
                job.Context.ffprobe,
                log = snapshotLog
            }), "application/json; charset=utf-8");
        }
        #endregion


        #region DOC
        [HttpGet("")]
        public IActionResult DOC()
        {
            var endpoints = new object[]
            {
                new {
                    path = "/transcoding/start.m3u8",
                    method = "GET",
                    query = new object[] {
                        new { name = "src", type = "string", required = true, description = "Source URL or local path to media" },
                        new { name = "a", type = "int", required = false, description = "Audio index (optional)" },
                        new { name = "s", type = "int", required = false, description = "Seek position in seconds (optional)" },
                        new { name = "subtitles", type = "bool", required = false, description = "subtitles on/off" },
                        new { name = "live", type = "bool", required = false, description = "Context live/playlist" }
                    },
                    description = "Start transcoding with query parameters and redirect to the generated HLS playlist"
                },
                new {
                    path = "/transcoding/start",
                    method = "POST",
                    contentType = "application/json",
                    body = new {
                        src = "https://example.com/media.mp4",
                        videoFormat = "",
                        live = false,
                        subtitles = false,
                        headers = new { referer = "https://example.com", userAgent = "HlsProxy/1.0" },
                        audio = AppInit.conf.transcoding.audioOptions,
                        hls = AppInit.conf.transcoding.hlsOptions
                    },
                    description = "Start transcoding by POSTing a JSON body. Returns StreamId and playlist URL"
                },
                new {
                    path = "/transcoding/{streamId}/live.m3u8",
                    method = "GET",
                    route = new { name = "streamId", type = "string", required = true },
                    description = "Returns the HLS live for the given transcoding job"
                },
                new {
                    path = "/transcoding/{streamId}/main.m3u8",
                    method = "GET",
                    route = new { name = "streamId", type = "string", required = true },
                    description = "Returns the HLS master/variant playlist for the given transcoding job"
                },
                new {
                    path = "/transcoding/{streamId}/{file}",
                    method = "GET",
                    route = new object[] {
                        new { name = "streamId", type = "string", required = true },
                        new { name = "file", type = "string", required = true, description = "Requested segment or asset (e.g. init.mp4, seg_1.m4s, index.m3u8)" }
                    },
                    description = "Serves individual segment files, init files and playlists produced by the transcoder. Supports range requests."
                },
                new {
                    path = "/transcoding/{streamId}/seek/{ss}",
                    method = "GET",
                    route = new object[] {
                        new { name = "streamId", type = "string", required = true },
                        new { name = "ss", type = "int", required = true, description = "Seek position in seconds (>= 0)" }
                    },
                    description = "Request the transcoder to seek to the specified position (async). Returns 200 on success."
                },
                new {
                    path = "/transcoding/{streamId}/heartbeat",
                    method = "GET",
                    route = new { name = "streamId", type = "string", required = true },
                    description = "Touch the job to keep it alive. Returns 200 if job exists."
                },
                new {
                    path = "/transcoding/{streamId}/stop",
                    method = "GET",
                    route = new { name = "streamId", type = "string", required = true },
                    description = "Stop the transcoding job. Returns 200 if stopped or 404 if job not found."
                },
                new {
                    path = "/transcoding/{streamId}/status",
                    method = "GET",
                    query = new { name = "log", type = "bool", required = false, description = "Include job log snapshot when true" },
                    route = new { name = "streamId", type = "string", required = true },
                    description = "Return current job status, uptime, position and optional log snapshot."
                }
            };

            return Ok(JsonConvert.SerializeObject(endpoints, Formatting.Indented));
        }
        #endregion
    }
}
