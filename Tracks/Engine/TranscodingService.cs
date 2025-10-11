using Shared;
using Shared.Models.AppConf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Tracks.Engine
{
    internal sealed class TranscodingService
    {
        private static readonly Lazy<TranscodingService> _lazy = new(() => new TranscodingService());

        private readonly ConcurrentDictionary<string, TranscodingJob> _jobs = new();
        private readonly object _configSync = new();
        private readonly Regex _safeFileNameRegex = new("^[A-Za-z0-9_.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private TracksTranscodingConf _config = new();
        private byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);
        private string _ffmpegPath = AppInit.Win32NT ? "data/ffmpeg.exe" : "ffmpeg";

        public static TranscodingService Instance => _lazy.Value;

        public void Configure(TracksTranscodingConf config)
        {
            if (config == null)
                return;

            lock (_configSync)
            {
                _config = config;
                _ffmpegPath = string.IsNullOrWhiteSpace(config.ffmpeg)
                    ? (AppInit.Win32NT ? "data/ffmpeg.exe" : (File.Exists("data/ffmpeg") ? "data/ffmpeg" : "ffmpeg"))
                    : config.ffmpeg;

                if (string.IsNullOrWhiteSpace(config.tempRoot))
                    config.tempRoot = Path.Combine(Path.GetTempPath(), "tracks-hls");

                try
                {
                    if (!Directory.Exists(config.tempRoot))
                    {
                        Directory.CreateDirectory(config.tempRoot);
                    }
                    else
                    {
                        foreach (var dir in Directory.GetDirectories(config.tempRoot))
                        {
                            try
                            {
                                Directory.Delete(dir, true);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }

        public ICollection<TranscodingJob> Jobs => _jobs.Values;

        public string FfmpegPath
        {
            get
            {
                lock (_configSync)
                    return _ffmpegPath;
            }
        }

        public int IdleTimeoutSeconds
        {
            get
            {
                lock (_configSync)
                    return _config.idleTimeoutSec;
            }
        }

        public (TranscodingJob job, string error) Start(TranscodingStartRequest request)
        {
            var config = GetConfig();
            if (!config.enable)
                return (null!, "Transcoding disabled");

            if (request == null || string.IsNullOrWhiteSpace(request.src))
                return (null!, "Source URL is required");

            if (_jobs.Count >= Math.Max(1, config.maxConcurrentJobs))
                return (null!, "Maximum concurrent jobs reached");

            if (!TryValidateSource(request.src, config, out var source, out var error))
                return (null!, error);

            var id = Guid.NewGuid().ToString("N");
            var streamId = BuildToken(id);

            var outputDir = Path.Combine(config.tempRoot!, id);
            Directory.CreateDirectory(outputDir);

            var context = new TranscodingStartContext(
                source,
                SanitizeHeader(GetHeader(request.headers, "userAgent"), "HlsProxy/1.0"),
                SanitizeHeader(GetHeader(request.headers, "referer")),
                MergeHlsOptions(config, request.hls),
                MergeAudioOptions(config, request.audio),
                request.subtitles,
                outputDir,
                Path.Combine(outputDir, "index.m3u8")
            );

            var process = CreateProcess(context);

            try
            {
                if (!process.Start())
                {
                    process.Dispose();
                    return (null!, "Failed to start ffmpeg");
                }
            }
            catch (Exception ex)
            {
                process.Dispose();
                return (null!, $"Failed to start ffmpeg: {ex.Message}");
            }

            var job = new TranscodingJob(id, streamId, outputDir, process, context);
            if (!_jobs.TryAdd(id, job))
            {
                try
                {
                    process.Kill(true);
                }
                catch { }

                process.Dispose();
                try
                {
                    if (Directory.Exists(outputDir))
                        Directory.Delete(outputDir, true);
                }
                catch { }
                return (null!, "Failed to register job");
            }

            _ = Task.Run(() => PumpStdErrAsync(job));
            _ = Task.Run(() => IdleWatchdogAsync(job, config));

            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => OnProcessExit(job);

            return (job, string.Empty);
        }

        public bool TryResolveJob(string streamId, out TranscodingJob job)
        {
            job = null!;
            if (string.IsNullOrWhiteSpace(streamId))
                return false;

            if (!TryParseToken(streamId, out var id))
                return false;

            if (!_jobs.TryGetValue(id, out job))
                return false;

            return true;
        }

        public async Task<bool> StopAsync(string streamId)
        {
            if (!TryResolveJob(streamId, out var job))
                return false;

            await StopJobAsync(job);
            return true;
        }

        public void Touch(TranscodingJob job) => job.UpdateLastAccess();

        public string GetFilePath(TranscodingJob job, string file)
        {
            if (string.IsNullOrWhiteSpace(file) || !_safeFileNameRegex.IsMatch(file))
                return null;

            var candidate = Path.Combine(job.OutputDirectory, file);
            if (!candidate.StartsWith(job.OutputDirectory, StringComparison.Ordinal))
                return null;

            return File.Exists(candidate) ? candidate : null;
        }

        private async Task StopJobAsync(TranscodingJob job, bool forced = false)
        {
            try
            {
                if (!job.Process.HasExited)
                {
                    if (forced)
                    {
                        job.Process.Kill(true);
                    }
                    else
                    {
                        try
                        {
                            await job.Process.StandardInput.WriteLineAsync("q");
                            await job.Process.StandardInput.FlushAsync();
                        }
                        catch { }

                        var waitTask = job.Process.WaitForExitAsync();
                        var timeout = Task.Delay(TimeSpan.FromMilliseconds(1500));
                        var completed = await Task.WhenAny(waitTask, timeout);
                        if (completed != waitTask)
                        {
                            try
                            {
                                job.Process.Kill(true);
                            }
                            catch { }

                            try
                            {
                                await job.Process.WaitForExitAsync();
                            }
                            catch { }
                        }
                        else
                        {
                            try
                            {
                                await waitTask;
                            }
                            catch { }
                        }
                    }
                }
            }
            finally
            {
                Cleanup(job);
            }
        }

        private void Cleanup(TranscodingJob job)
        {
            var removed = _jobs.TryRemove(job.Id, out _);

            job.StopBackground();
            job.SignalExit();

            if (!removed)
                return;

            try
            {
                if (Directory.Exists(job.OutputDirectory))
                    Directory.Delete(job.OutputDirectory, true);
            }
            catch { }

            job.Dispose();
        }

        private TracksTranscodingConf GetConfig()
        {
            lock (_configSync)
                return _config;
        }

        private static string GetHeader(Dictionary<string, string> headers, string key)
        {
            if (headers == null || headers.Count == 0)
                return null;

            foreach (var (k, v) in headers)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return v;
            }

            return null;
        }

        private static TranscodingHlsOptions MergeHlsOptions(TracksTranscodingConf config, TranscodingHlsOptions request)
        {
            var opt = request ?? config.hlsOptions;

            return new TranscodingHlsOptions
            {
                segDur = opt?.segDur > 1 ? opt.segDur : 1,
                winSize = opt?.winSize > 5 ? opt.winSize : 5,
                fmp4 = opt?.fmp4 ?? true
            };
        }

        private static TranscodingAudioOptions MergeAudioOptions(TracksTranscodingConf config, TranscodingAudioOptions request)
        {
            var opt = request ?? config.audioOptions;

            return new TranscodingAudioOptions
            {
                bitrateKbps = opt?.bitrateKbps is > 0 and <= 512 ? opt.bitrateKbps : 160,
                stereo = opt?.stereo ?? true,
                transcodeToAac = opt?.transcodeToAac ?? true
            };
        }

        private static bool TryValidateSource(string src, TracksTranscodingConf config, out Uri uri, out string error)
        {
            error = string.Empty;
            uri = null!;

            if (!Uri.TryCreate(src, UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "Only http/https URLs are allowed";
                return false;
            }

            if (config.allowHosts != null && config.allowHosts.Length > 0)
            {
                if (!config.allowHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
                {
                    error = "Source host is not allowed";
                    return false;
                }
            }

            return true;
        }

        public void StopAll()
        {
            var jobs = _jobs.Values.ToArray();
            foreach (var job in jobs)
            {
                try
                {
                    _ = StopJobAsync(job, forced: true).ConfigureAwait(false);
                }
                catch { }
            }
        }

        private Process CreateProcess(TranscodingStartContext context)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = false,
                    CreateNoWindow = true
                }
            };

            var args = process.StartInfo.ArgumentList;

            /*
•	-hide_banner — отключает вывод баннера ffmpeg (версия/конфигурация) в stderr, чтобы логи были чище.
•	-user_agent + context.UserAgent — задаёт заголовок User-Agent для HTTP-запросов к входному URL.
•	-headers + Referer: {context.Referer}&#x0a; — добавляет произвольные HTTP-заголовки (здесь: Referer) при обращении к входному URL.
•	-re — читает входной поток в реальном (реальном времени) темпе; полезно при трансляции/стриминге, чтобы не читать вход быстрее реального времени.
•	-threads 0 — позволяет ffmpeg автоматически выбрать количество потоков (CPU) для кодирования/декодирования.
•	-fflags +genpts — устанавливает флаг форматов: +genpts генерирует PTS для фреймов, если их нет (избегает проблем с отсутствующими временными метками).
•	-i {Source} — указывает входной источник (URL/файл).
•	-map 0:v:0 — маппит первый видеопоток первого входа в выход.
•	-map 0:a:0 — маппит первый аудиопоток первого входа в выход.
•	-sn (опционально, при subtitles == false) — отключает включение субтитров в вывод.
•	-dn — отключает включение data-потоков (metadata/data tracks).
•	-map_metadata -1 — не копировать глобальные метаданные в выход (очищает метаданные).
•	-map_chapters -1 — не копировать главы в выход (удаляет chapters).
•	-c:v copy — видео «без перекодирования» (копируется поток как есть), чтобы избежать нагрузки и потери качества.

•	аудио:
•	если transcodeToAac:
•	-c:a aac — кодировать аудио в AAC.
•	-ac 2 / -ac 1 — установить число каналов (стерео/моно) согласно настройке.
•	-b:a {N}k — задать битрейт аудио (ограничен/откорректирован в коде).
•	-profile:a aac_low — указать профиль AAC (обычно LC/AAC для совместимости и низкой задержки).
•	иначе -c:a copy — копировать аудиопоток без перекодирования.

•	-avoid_negative_ts disabled — не применять сдвиг временных меток для предотвращения отрицательных TS; обычно используется чтобы сохранить исходные PTS при сегментации HLS.
•	-max_muxing_queue_size 2048 — увеличить очередь пакетов при мультиплексировании; предотвращает ошибки вроде Too many packets buffered при больших пиковых нагрузках.
•	-f hls — установить формат выхода в HLS (HTTP Live Streaming).
•	-max_delay 5000000 — максимальная задержка буфера (микросекунды/зависит от контекста) — снижает вероятность чрезмерного буферинга/задержки при входном потоке.
•	-hls_segment_type fmp4 или mpegts — тип сегмента HLS: fMP4 (CMAF) или MPEG-TS. Выбор влияет на контейнер сегментов.
•	при mpegts: -bsf:v h264_mp4toannexb — битстрим-фильтр, который конвертирует H.264 из MP4-контейнера в Annex-B формат, требуемый для TS.
•	-hls_time {segDur} — продолжительность каждого HLS-сегмента в секундах.
•	-hls_flags append_list+omit_endlist — флаги HLS:
•	append_list — позволяет дописывать записи в плейлист (поведение при динамическом добавлении сегментов);
•	omit_endlist — не включать #EXT-X-ENDLIST, чтобы плейлист считался живым (live).
•	-hls_list_size {winSize} — число записей в плейлисте (оконный размер, сколько последних сегментов видит клиент).
•	-master_pl_name index.m3u8 — имя master-плейлиста (если используется).
•	-hls_fmp4_init_filename {init.mp4} — имя init-сегмента для fMP4 (инициализационный фрагмент).
•	-hls_segment_filename {seg_%05d.m4s / seg_%05d.ts} — шаблон имени файлов сегментов (с порядковым номером).
•	-y {PlaylistPath} — перезаписать выходной файл без запроса подтверждения; указывает путь финального плейлиста/файла.
             */

            args.Add("-hide_banner");

            args.Add("-user_agent");
            args.Add(context.UserAgent);
            if (!string.IsNullOrWhiteSpace(context.Referer))
            {
                args.Add("-headers");
                args.Add($"Referer: {context.Referer}\\r\\n");
            }

            args.Add("-re");

            if (context.HlsOptions.seek > 0)
            {
                args.Add("-ss");
                args.Add(context.HlsOptions.seek.ToString());
            }

            args.Add("-threads");
            args.Add("0");

            args.Add("-fflags");
            args.Add("+genpts");

            args.Add("-i");
            args.Add(context.Source.AbsoluteUri);

            args.Add("-map");
            args.Add("0:v:0");

            args.Add("-map");
            args.Add($"{context.Audio.index}:a:0");

            if (context.subtitles == false)
                args.Add("-sn");

            args.Add("-dn");

            args.Add("-map_metadata");
            args.Add("-1");

            args.Add("-map_chapters");
            args.Add("-1");

            args.Add("-c:v");
            args.Add("copy");

            if (context.Audio.transcodeToAac)
            {
                args.Add("-c:a");
                args.Add("aac");
                args.Add("-ac");
                args.Add(context.Audio.stereo ? "2" : "1");
                args.Add("-b:a");
                args.Add($"{Math.Clamp(context.Audio.bitrateKbps, 32, 512)}k");
                args.Add("-profile:a");
                args.Add("aac_low");
            }
            else
            {
                args.Add("-c:a");
                args.Add("copy");
            }

            args.Add("-avoid_negative_ts");
            args.Add("disabled");

            args.Add("-max_muxing_queue_size");
            args.Add("2048");

            args.Add("-f");
            args.Add("hls");

            args.Add("-max_delay");
            args.Add("5000000");

            args.Add("-hls_segment_type");

            if (context.HlsOptions.fmp4)
            {
                args.Add("fmp4");
            }
            else
            {
                args.Add("mpegts");
                args.Add("-bsf:v");
                args.Add("h264_mp4toannexb");
            }

            args.Add("-hls_time");
            args.Add(context.HlsOptions.segDur.ToString(CultureInfo.InvariantCulture));

            args.Add("-hls_flags");
            args.Add("append_list+omit_endlist+delete_segments");

            args.Add("-hls_list_size");
            args.Add(context.HlsOptions.winSize.ToString(CultureInfo.InvariantCulture));

            args.Add("-master_pl_name");
            args.Add("index.m3u8");

            args.Add("-hls_fmp4_init_filename");
            args.Add(Path.Combine(context.OutputDirectory, "init.mp4"));

            args.Add("-hls_segment_filename");
            args.Add(Path.Combine(context.OutputDirectory, context.HlsOptions.fmp4 ? "seg_%05d.m4s" : "seg_%05d.ts"));

            args.Add("-y");
            args.Add(context.PlaylistPath);

            return process;
        }

        private async Task PumpStdErrAsync(TranscodingJob job)
        {
            try
            {
                while (!job.Process.StandardError.EndOfStream)
                {
                    var line = await job.Process.StandardError.ReadLineAsync();
                    if (line == null)
                        break;

                    job.AppendLog(line);
                }
            }
            catch { }
        }

        private async Task IdleWatchdogAsync(TranscodingJob job, TracksTranscodingConf config)
        {
            var idle = TimeSpan.FromSeconds(Math.Max(20, config.idleTimeoutSec));

            try
            {
                while (!job.CancellationToken.IsCancellationRequested && !job.Process.HasExited)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), job.CancellationToken);

                    if (DateTime.UtcNow - job.LastAccessUtc > idle)
                    {
                        await StopJobAsync(job);
                        break;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
        }

        private void OnProcessExit(TranscodingJob job)
        {
            job.SignalExit();
            Cleanup(job);
        }

        private bool TryParseToken(string streamId, out string id)
        {
            id = string.Empty;
            var parts = streamId.Split('.', 2);
            if (parts.Length != 2)
                return false;

            id = parts[0];
            if (id.Length != 32)
                return false;

            try
            {
                var padded = parts[1].PadRight(parts[1].Length + (4 - parts[1].Length % 4) % 4, '=');
                var hmac = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
                var expected = ComputeHmac(id);
                if (!CryptographicOperations.FixedTimeEquals(hmac, expected))
                    return false;
            }
            catch
            {
                return false;
            }

            return true;
        }

        private string BuildToken(string id)
        {
            var mac = ComputeHmac(id);
            var b64 = Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{id}.{b64}";
        }

        private byte[] ComputeHmac(string id)
        {
            using var hmac = new HMACSHA256(_hmacKey);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(id));
        }

        private static string SanitizeHeader(string value, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var clean = value.Replace("\r", string.Empty).Replace("\n", string.Empty);
            return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
        }
    }
}
