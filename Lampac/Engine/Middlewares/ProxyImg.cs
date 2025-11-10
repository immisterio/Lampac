using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared;
using Shared.Engine;
using Shared.Models;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyImg
    {
        #region ProxyImg
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, byte> cacheFiles = new ConcurrentDictionary<string, byte>();

        static readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphoreLocks = new();

        static Timer cleanupTimer;

        static ProxyImg()
        {
            if (AppInit.conf.multiaccess == false)
                return;

            Directory.CreateDirectory("cache/img");

            foreach (var item in Directory.EnumerateFiles("cache/img", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), 0);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/img",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };

            cleanupTimer = new Timer(cleanup, null, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));
        }

        static void cleanup(object state)
        {
            try
            {
                var files = Directory.GetFiles("cache/img", "*").Select(f => Path.GetFileName(f)).ToHashSet();

                foreach (string md5fileName in cacheFiles.Keys.ToArray())
                {
                    if (!files.Contains(md5fileName))
                        cacheFiles.TryRemove(md5fileName, out _);
                }
            }
            catch { }
        }

        public ProxyImg(RequestDelegate next) { }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache)
        {
            using (var ctsHttp = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted))
            {
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(90));

                var requestInfo = httpContext.Features.Get<RequestModel>();

                var init = AppInit.conf.serverproxy.image;
                bool cacheimg = init.cache && AppInit.conf.mikrotik == false;

                string servPath = Regex.Replace(httpContext.Request.Path.Value, "/proxyimg([^/]+)?/", "");
                string href = servPath + httpContext.Request.QueryString.Value;

                #region Проверки
                if (servPath.Contains("image.tmdb.org"))
                {
                    httpContext.Response.Redirect($"/tmdb/img/{Regex.Replace(href.Replace("://", ":/_/").Replace("//", "/").Replace(":/_/", "://"), "^https?://[^/]+/", "")}");
                    return;
                }

                var decryptLink = ProxyLink.Decrypt(servPath, requestInfo.IP);

                if (AppInit.conf.serverproxy.encrypt || decryptLink?.uri != null)
                {
                    href = decryptLink?.uri;
                }
                else
                {
                    if (!AppInit.conf.serverproxy.enable)
                    {
                        httpContext.Response.StatusCode = 403;
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(href) || !href.StartsWith("http"))
                {
                    httpContext.Response.StatusCode = 404;
                    return;
                }
                #endregion

                if (AppInit.conf.serverproxy.showOrigUri)
                    httpContext.Response.Headers["PX-Orig"] = href;

                #region width / height
                int width = 0;
                int height = 0;

                if (httpContext.Request.Path.Value.StartsWith("/proxyimg:"))
                {
                    if (!cacheimg)
                        cacheimg = init.cache_rsize;

                    var gimg = Regex.Match(httpContext.Request.Path.Value, "/proxyimg:([0-9]+):([0-9]+)").Groups;
                    width = int.Parse(gimg[1].Value);
                    height = int.Parse(gimg[2].Value);
                }
                #endregion

                string md5key = CrypTo.md5($"{href}:{width}:{height}");
                string outFile = Path.Combine("cache", "img", md5key);

                string url_reserve = null;
                if (href.Contains(" or "))
                {
                    var urls = href.Split(" or ");
                    href = urls[0];
                    url_reserve = urls[1];
                }

                string contentType = href.Contains(".png") ? "image/png" : href.Contains(".webp") ? "image/webp" : "image/jpeg";
                if (width > 0 || height > 0)
                    contentType = href.Contains(".png") ? "image/png" : "image/jpeg";

                if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                {
                    httpContext.Response.Headers["X-Cache-Status"] = "HIT";
                    httpContext.Response.ContentType = contentType;
                    await httpContext.Response.SendFileAsync(outFile);
                    return;
                }

                var semaphore = cacheimg ? _semaphoreLocks.GetOrAdd(href, _ => new SemaphoreSlim(1, 1)) : null;

                try
                {
                    string memKeyErrorDownload = $"ProxyImg:ErrorDownload:{href}";
                    if (memoryCache.TryGetValue(memKeyErrorDownload, out _))
                    {
                        httpContext.Response.Redirect(href);
                        return;
                    }

                    if (semaphore != null)
                        await semaphore.WaitAsync(TimeSpan.FromMinutes(1));

                    if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                    {
                        httpContext.Response.Headers["X-Cache-Status"] = "HIT";
                        httpContext.Response.ContentType = contentType;
                        await httpContext.Response.SendFileAsync(outFile);
                        return;
                    }

                    httpContext.Response.Headers["X-Cache-Status"] = cacheimg ? "MISS" : "bypass";

                    var proxyManager = decryptLink?.plugin == "posterapi" ? new ProxyManager("posterapi", AppInit.conf.posterApi) : new ProxyManager("proxyimg", init);
                    var proxy = proxyManager.Get();

                    if (width == 0 && height == 0)
                    {
                        #region bypass
                        bypass_reset:
                        var handler = Http.Handler(href, proxy);

                        var client = FrendlyHttp.HttpMessageClient("base", handler);

                        var req = new HttpRequestMessage(HttpMethod.Get, href)
                        {
                            Version = HttpVersion.Version11
                        };

                        Http.DefaultRequestHeaders(href, req, null, null, decryptLink?.headers);

                        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                        {
                            using (HttpResponseMessage response = await client.SendAsync(req, cts.Token))
                            {
                                if (response.StatusCode != HttpStatusCode.OK)
                                {
                                    if (url_reserve != null)
                                    {
                                        href = url_reserve;
                                        url_reserve = null;
                                        goto bypass_reset;
                                    }

                                    if (cacheimg)
                                        memoryCache.Set(memKeyErrorDownload, 0, DateTime.Now.AddSeconds(5));

                                    proxyManager.Refresh();
                                    httpContext.Response.Redirect(href);
                                    return;
                                }

                                httpContext.Response.StatusCode = (int)response.StatusCode;

                                if (response.Headers.TryGetValues("Content-Type", out var contype))
                                    httpContext.Response.ContentType = contype?.FirstOrDefault() ?? contentType;

                                if (cacheimg)
                                {
                                    int initialCapacity = response.Content.Headers.ContentLength.HasValue ?
                                        (int)response.Content.Headers.ContentLength.Value :
                                        50_000; // 50kB

                                    using (var memoryStream = new MemoryStream(initialCapacity))
                                    {
                                        try
                                        {
                                            bool saveCache = true;

                                            using (var responseStream = await response.Content.ReadAsStreamAsync())
                                            {
                                                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                                                try
                                                {
                                                    int bytesRead;

                                                    while ((bytesRead = await responseStream.ReadAsync(buffer, ctsHttp.Token)) > 0)
                                                    {
                                                        memoryStream.Write(buffer, 0, bytesRead);
                                                        await httpContext.Response.Body.WriteAsync(buffer, 0, bytesRead, ctsHttp.Token);
                                                    }
                                                }
                                                catch
                                                {
                                                    saveCache = false;
                                                }
                                                finally
                                                {
                                                    ArrayPool<byte>.Shared.Return(buffer);
                                                }
                                            }

                                            if (saveCache && memoryStream.Length > 1000)
                                            {
                                                try
                                                {
                                                    if (cacheFiles.ContainsKey(md5key) == false || (AppInit.conf.multiaccess == false && File.Exists(outFile) == false))
                                                    {
                                                        File.WriteAllBytes(outFile, memoryStream.ToArray());

                                                        if (AppInit.conf.multiaccess)
                                                            cacheFiles.TryAdd(md5key, 0);
                                                    }
                                                }
                                                catch { File.Delete(outFile); }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                                else
                                {
                                    await response.Content.CopyToAsync(httpContext.Response.Body, ctsHttp.Token);
                                }
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region rsize
                        rsize_reset:
                        var array = await Download(href, proxy: proxy, headers: decryptLink?.headers);
                        if (array == null)
                        {
                            if (url_reserve != null)
                            {
                                href = url_reserve;
                                url_reserve = null;
                                goto rsize_reset;
                            }

                            if (cacheimg)
                                memoryCache.Set(memKeyErrorDownload, 0, DateTime.Now.AddSeconds(5));

                            proxyManager.Refresh();
                            httpContext.Response.Redirect(href);
                            return;
                        }

                        if (array.Length > 1000)
                        {
                            if (AppInit.conf.imagelibrary == "NetVips")
                            {
                                array = NetVipsImage(href, array, width, height);
                            }
                            else if (AppInit.conf.imagelibrary == "ImageMagick" && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            {
                                array = ImageMagick(array, width, height, cacheimg ? outFile : null);
                            }
                        }

                        if (array.Length > 1000 && cacheimg)
                        {
                            try
                            {
                                if (cacheFiles.ContainsKey(md5key) == false || (AppInit.conf.multiaccess == false && File.Exists(outFile) == false))
                                {
                                    File.WriteAllBytes(outFile, array);

                                    if (AppInit.conf.multiaccess)
                                        cacheFiles.TryAdd(md5key, 0);
                                }
                            }
                            catch { try { File.Delete(outFile); } catch { } }
                        }

                        proxyManager.Success();
                        httpContext.Response.ContentType = contentType;
                        await httpContext.Response.Body.WriteAsync(array, ctsHttp.Token);
                        #endregion
                    }
                }
                finally
                {
                    if (semaphore != null)
                    {
                        try
                        {
                            semaphore.Release();
                        }
                        finally
                        {
                            if (semaphore.CurrentCount == 1)
                                _semaphoreLocks.TryRemove(href, out _);
                        }
                    }
                }
            }
        }


        #region Download
        async Task<byte[]> Download(string url, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = Http.Handler(url, proxy);

                var client = FrendlyHttp.HttpMessageClient("base", handler);

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version11
                };

                if (headers != null)
                {
                    foreach (var h in headers)
                        req.Headers.TryAddWithoutValidation(h.name, h.val);
                }

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                {
                    using (HttpResponseMessage response = await client.SendAsync(req, cts.Token))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            return null;

                        using (HttpContent content = response.Content)
                        {
                            byte[] res = await content.ReadAsByteArrayAsync();
                            if (res == null || res.Length == 0)
                                return null;

                            return res;
                        }
                    }
                }
            }
            catch
            {
                return null;
            }
        }
        #endregion

        #region NetVipsImage
        private byte[] NetVipsImage(string href, byte[] array, int width, int height)
        {
            try
            {
                using (var image = NetVips.Image.NewFromBuffer(array))
                {
                    if ((width != 0 && image.Width > width) || (height != 0 && image.Height > height))
                    {
                        using (var res = image.ThumbnailImage(width == 0 ? image.Width : width, height == 0 ? image.Height : height, crop: NetVips.Enums.Interesting.None))
                        {
                            var buffer = href.Contains(".png") ? res.PngsaveBuffer() : res.JpegsaveBuffer();
                            if (buffer != null && buffer.Length > 1000)
                            {
                                array = null;
                                return buffer;
                            }
                        }
                    }
                }
            }
            catch { }

            return array;
        }
        #endregion

        #region ImageMagick
        static string imaGikPath = null;

        /// <summary>
        /// apt install -y imagemagick libpng-dev libjpeg-dev libwebp-dev
        /// </summary>
        static byte[] ImageMagick(byte[] array, int width, int height, string myoutputFilePath)
        {
            string inputFilePath = null;
            string outputFilePath = null;

            if (Directory.Exists("/dev/shm"))
            {
                inputFilePath = $"/dev/shm/{CrypTo.md5(DateTime.Now.ToBinary().ToString())}.in";
                outputFilePath = myoutputFilePath ?? $"/dev/shm/{CrypTo.md5(DateTime.Now.ToBinary().ToString())}.out";
            }

            if (inputFilePath == null)
                inputFilePath = Path.GetTempFileName();

            if (outputFilePath == null) 
                outputFilePath = myoutputFilePath ?? Path.GetTempFileName();

            if (imaGikPath == null)
                imaGikPath = File.Exists("/usr/bin/magick") ? "magick" : "convert";

            try
            {
                File.WriteAllBytes(inputFilePath, array);

                string argsize = width > 0 && height > 0 ? $"{width}x{height}" : width > 0 ? $"{width}x" : $"x{height}";

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = imaGikPath;
                    process.StartInfo.Arguments = $"\"{inputFilePath}\" -resize {argsize} \"{outputFilePath}\"";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        return array;
                }

                return File.ReadAllBytes(outputFilePath);
            }
            catch 
            { 
                return array; 
            }
            finally
            {
                try
                {
                    if (File.Exists(inputFilePath))
                        File.Delete(inputFilePath);

                    if (File.Exists(outputFilePath) && myoutputFilePath != outputFilePath)
                        File.Delete(outputFilePath);
                }
                catch { }
            }
        }
        #endregion
    }
}
