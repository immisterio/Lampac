using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IO;
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
        static readonly FileSystemWatcher fileWatcher;

        static readonly ConcurrentDictionary<string, int> cacheFiles = new();

        public static int Stat_ContCacheFiles => cacheFiles.IsEmpty ? 0 : cacheFiles.Count;

        static readonly Timer cleanupTimer;

        static ProxyImg()
        {
            Directory.CreateDirectory("cache/img");

            if (AppInit.conf.multiaccess == false)
                return;

            foreach (var item in Directory.EnumerateFiles("cache/img", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), (int)new FileInfo(item).Length);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/img",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            //fileWatcher.Created += (s, e) => 
            //{ 
            //    cacheFiles.TryAdd(e.Name, 0); 
            //};

            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };

            cleanupTimer = new Timer(cleanup, null, TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60));
        }

        static void cleanup(object state)
        {
            try
            {
                string[] files = Directory.GetFiles("cache/img", "*")
                    .Select(Path.GetFileName)
                    .ToArray();

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
                ctsHttp.CancelAfter(TimeSpan.FromSeconds(30));

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

                if (InvkEvent.IsProxyImgMd5key())
                    InvkEvent.ProxyImgMd5key(ref md5key, httpContext, requestInfo, decryptLink, href, width, height);

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

                #region cacheFiles
                if (cacheimg)
                {
                    if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                    {
                        httpContext.Response.Headers["X-Cache-Status"] = "HIT";
                        httpContext.Response.ContentType = contentType;

                        if (AppInit.conf.serverproxy.responseContentLength && cacheFiles.ContainsKey(md5key))
                            httpContext.Response.ContentLength = cacheFiles[md5key];

                        await httpContext.Response.SendFileAsync(outFile, ctsHttp.Token).ConfigureAwait(false);
                        return;
                    }
                }
                #endregion

                var semaphore = cacheimg ?  new SemaphorManager(outFile, ctsHttp.Token) : null;

                try
                {
                    string memKeyErrorDownload = $"ProxyImg:ErrorDownload:{href}";
                    if (memoryCache.TryGetValue(memKeyErrorDownload, out _))
                    {
                        httpContext.Response.Redirect(href);
                        return;
                    }

                    if (semaphore != null)
                        await semaphore.WaitAsync().ConfigureAwait(false);

                    #region cacheFiles
                    if (cacheimg)
                    {
                        if (cacheFiles.ContainsKey(md5key) || (AppInit.conf.multiaccess == false && File.Exists(outFile)))
                        {
                            httpContext.Response.Headers["X-Cache-Status"] = "HIT";
                            httpContext.Response.ContentType = contentType;

                            if (AppInit.conf.serverproxy.responseContentLength && cacheFiles.ContainsKey(md5key))
                                httpContext.Response.ContentLength = cacheFiles[md5key];

                            semaphore?.Release();
                            await httpContext.Response.SendFileAsync(outFile, ctsHttp.Token).ConfigureAwait(false);
                            return;
                        }
                    }
                    #endregion

                    httpContext.Response.Headers["X-Cache-Status"] = cacheimg ? "MISS" : "bypass";

                    #region proxyManager
                    ProxyManager proxyManager = null;

                    if (decryptLink?.plugin == "posterapi" && AppInit.conf.posterApi.useproxy)
                        proxyManager = new ProxyManager("posterapi", AppInit.conf.posterApi);

                    if (proxyManager == null && init.useproxy)
                        proxyManager = new ProxyManager("proxyimg", init);

                    WebProxy proxy = proxyManager?.Get();
                    #endregion

                    if (width == 0 && height == 0)
                    {
                        #region bypass
                        bypass_reset:
                        var handler = Http.Handler(href, proxy);

                        var client = FrendlyHttp.MessageClient("proxyimg", handler);

                        var req = new HttpRequestMessage(HttpMethod.Get, href)
                        {
                            Version = HttpVersion.Version11
                        };

                        bool useDefaultHeaders = true;
                        if (decryptLink?.headers != null && decryptLink.headers.Count > 0 && decryptLink.headers.FirstOrDefault(i => i.name.ToLower() == "user-agent") != null)
                            useDefaultHeaders = false;

                        Http.DefaultRequestHeaders(href, req, null, null, decryptLink?.headers, useDefaultHeaders: useDefaultHeaders);

                        using (HttpResponseMessage response = await client.SendAsync(req, ctsHttp.Token).ConfigureAwait(false))
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

                                proxyManager?.Refresh();
                                httpContext.Response.Redirect(href);
                                return;
                            }

                            httpContext.Response.StatusCode = (int)response.StatusCode;

                            if (response.Content.Headers.TryGetValues("Content-Type", out var contype))
                                httpContext.Response.ContentType = contype?.FirstOrDefault()?.ToLower()?.Trim() ?? contentType;
                            else
                                httpContext.Response.ContentType = contentType;

                            if (AppInit.conf.serverproxy.responseContentLength && response.Content?.Headers?.ContentLength > 0)
                            {
                                if (!AppInit.CompressionMimeTypes.Contains(httpContext.Response.ContentType))
                                    httpContext.Response.ContentLength = response.Content.Headers.ContentLength.Value;
                            }

                            if (cacheimg)
                            {
                                byte[] buffer = ArrayPool<byte>.Shared.Rent(PoolInvk.rentChunk);

                                try
                                {
                                    int cacheLength = 0;

                                    using (var cacheStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None, PoolInvk.bufferSize))
                                    {
                                        using (var responseStream = await response.Content.ReadAsStreamAsync(ctsHttp.Token).ConfigureAwait(false))
                                        {
                                            int bytesRead;

                                            while ((bytesRead = await responseStream.ReadAsync(buffer, ctsHttp.Token).ConfigureAwait(false)) > 0)
                                            {
                                                cacheLength += bytesRead;
                                                await cacheStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                                                await httpContext.Response.Body.WriteAsync(buffer, 0, bytesRead, ctsHttp.Token).ConfigureAwait(false);
                                            }
                                        }
                                    }

                                    if (!response.Content.Headers.ContentLength.HasValue || response.Content.Headers.ContentLength.Value == cacheLength)
                                    {
                                        if (AppInit.conf.multiaccess)
                                            cacheFiles[md5key] = cacheLength;
                                    }
                                    else
                                    {
                                        File.Delete(outFile);
                                    }
                                }
                                catch
                                {
                                    File.Delete(outFile);
                                    throw;
                                }
                                finally
                                {
                                    ArrayPool<byte>.Shared.Return(buffer);
                                }
                            }
                            else
                            {
                                await response.Content.CopyToAsync(httpContext.Response.Body, ctsHttp.Token).ConfigureAwait(false);
                            }
                        }
                        #endregion
                    }
                    else
                    {
                        #region rsize
                        rsize_reset:

                        using (var inArray = PoolInvk.msm.GetStream())
                        {
                            var result = await Download(inArray, href, ctsHttp.Token, proxy: proxy, headers: decryptLink?.headers).ConfigureAwait(false);

                            if (!result.success)
                            {
                                if (url_reserve != null)
                                {
                                    href = url_reserve;
                                    url_reserve = null;
                                    goto rsize_reset;
                                }

                                if (cacheimg)
                                    memoryCache.Set(memKeyErrorDownload, 0, DateTime.Now.AddSeconds(5));

                                proxyManager?.Refresh();
                                httpContext.Response.Redirect(href);
                                return;
                            }

                            using (var outArray = PoolInvk.msm.GetStream())
                            {
                                bool successConvert = false;

                                if ((result.contentType ?? contentType) is "image/png" or "image/webp" or "image/jpeg")
                                {
                                    if (AppInit.conf.imagelibrary == "NetVips")
                                    {
                                        successConvert = NetVipsImage(href, inArray, outArray, width, height);
                                    }
                                    else if (AppInit.conf.imagelibrary == "ImageMagick" && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                                    {
                                        successConvert = await ImageMagick(inArray, outArray, width, height, cacheimg ? outFile : null);
                                    }
                                }

                                var resultArray = successConvert ? outArray : inArray;
                                resultArray.Position = 0;

                                if (successConvert)
                                    proxyManager?.Success();

                                httpContext.Response.ContentType = contentType;

                                if (AppInit.conf.serverproxy.responseContentLength)
                                    httpContext.Response.ContentLength = resultArray.Length;

                                try
                                {
                                    await resultArray.CopyToAsync(httpContext.Response.Body, PoolInvk.bufferSize, ctsHttp.Token).ConfigureAwait(false);

                                    if (cacheimg)
                                        await TrySaveCache(resultArray, outFile, md5key);
                                }
                                catch
                                {
                                    if (cacheimg)
                                        await TrySaveCache(resultArray, outFile, md5key);

                                    throw;
                                }
                            }
                        }
                        #endregion
                    }
                }
                finally
                {
                    if (semaphore != null)
                        semaphore.Release();
                }
            }
        }


        #region Download
        async Task<(bool success, string contentType)> Download(RecyclableMemoryStream ms, string url, CancellationToken cancellationToken, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = Http.Handler(url, proxy);

                var client = FrendlyHttp.MessageClient("base", handler);

                var req = new HttpRequestMessage(HttpMethod.Get, url)
                {
                    Version = HttpVersion.Version11
                };


                bool useDefaultHeaders = true;
                if (headers != null && headers.Count > 0 && headers.FirstOrDefault(i => i.name.ToLowerInvariant() == "user-agent") != null)
                    useDefaultHeaders = false;

                Http.DefaultRequestHeaders(url, req, null, null, headers, useDefaultHeaders: useDefaultHeaders);

                using (HttpResponseMessage response = await client.SendAsync(req, cancellationToken).ConfigureAwait(false))
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                        return default;

                    using (HttpContent content = response.Content)
                    {
                        await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
                        if (ms.Length == 0 || 1000 > ms.Length)
                            return default;

                        if (content.Headers != null)
                        {
                            if (content.Headers.ContentLength.HasValue && content.Headers.ContentLength != ms.Length)
                                return default;

                            response.Content.Headers.TryGetValues("Content-Type", out var _contentType);

                            ms.Position = 0;
                            return (true, _contentType?.FirstOrDefault()?.ToLower());
                        }

                        ms.Position = 0;
                        return (true, null);
                    }
                }
            }
            catch
            {
                return default;
            }
        }
        #endregion

        #region TrySaveCache
        async Task TrySaveCache(RecyclableMemoryStream ms, string outFile, string md5key)
        {
            try
            {
                ms.Position = 0;

                using (var streamFile = File.OpenWrite(outFile))
                    await ms.CopyToAsync(streamFile, PoolInvk.bufferSize);

                if (AppInit.conf.multiaccess)
                    cacheFiles[md5key] = (int)ms.Length;
            }
            catch { File.Delete(outFile); }
        }
        #endregion

        #region NetVipsImage
        private bool NetVipsImage(string href, RecyclableMemoryStream inArray, RecyclableMemoryStream outArray, int width, int height)
        {
            try
            {
                using (var image = NetVips.Image.NewFromStream(inArray, access: NetVips.Enums.Access.Sequential))
                {
                    if ((width != 0 && image.Width > width) || (height != 0 && image.Height > height))
                    {
                        using (var res = image.ThumbnailImage(width == 0 ? image.Width : width, height == 0 ? image.Height : height, crop: NetVips.Enums.Interesting.None))
                        {
                            if (href.Contains(".png"))
                                res.PngsaveStream(outArray);
                            else
                                res.JpegsaveStream(outArray);

                            if (outArray.Length > 1000)
                                return true;
                        }
                    }
                }
            }
            catch { }

            return false;
        }
        #endregion

        #region ImageMagick
        static string imaGikPath = null;

        /// <summary>
        /// apt install -y imagemagick libpng-dev libjpeg-dev libwebp-dev
        /// </summary>
        async static Task<bool> ImageMagick(RecyclableMemoryStream inArray, RecyclableMemoryStream outArray, int width, int height, string myoutputFilePath)
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
                inArray.Position = 0;

                using (var streamFile = File.OpenWrite(inputFilePath))
                    await inArray.CopyToAsync(streamFile, PoolInvk.bufferSize);

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
                        return false;
                }

                using (var streamFile = File.OpenRead(outputFilePath))
                    await streamFile.CopyToAsync(outArray, PoolInvk.bufferSize);

                return true;
            }
            catch 
            { 
                return false; 
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
