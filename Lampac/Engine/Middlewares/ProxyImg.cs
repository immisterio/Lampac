using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using Shared.Model.Online;
using Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyImg
    {
        #region ProxyImg
        private readonly RequestDelegate _next;

        private readonly IHttpClientFactory _httpClientFactory;

        public ProxyImg(RequestDelegate next, IHttpClientFactory httpClientFactory)
        {
            _next = next;
            _httpClientFactory = httpClientFactory;
        }

        static ProxyImg()
        {
            Directory.CreateDirectory("cache/img");
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxyimg"))
            {
                var requestInfo = httpContext.Features.Get<RequestModel>();

                var init = AppInit.conf.serverproxy.image;
                bool cacheimg = init.cache;

                #region Проверки
                string href = Regex.Replace(httpContext.Request.Path.Value, "/proxyimg([^/]+)?/", "") + httpContext.Request.QueryString.Value;

                if (href.Contains("image.tmdb.org"))
                {
                    httpContext.Response.Redirect($"/tmdb/img/{Regex.Replace(href.Replace("://", ":/_/").Replace("//", "/").Replace(":/_/", "://"), "^https?://[^/]+/", "")}");
                    return;
                }

                var decryptLink = ProxyLink.Decrypt(Regex.Replace(href, "(\\?|&).*", ""), requestInfo.IP);

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
                    httpContext.Response.Headers.Add("PX-Orig", href);

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
                string outFile = $"cache/img/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";

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

                if (cacheimg && File.Exists(outFile))
                {
                    httpContext.Response.ContentType = contentType;
                    httpContext.Response.Headers.Add("X-Cache-Status", "HIT");

                    using (var fs = new FileStream(outFile, FileMode.Open, FileAccess.Read))
                        await fs.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);

                    return;
                }

                string memKeyErrorDownload = $"ProxyImg:ErrorDownload:{href}";
                if (memoryCache.TryGetValue(memKeyErrorDownload, out _))
                {
                    httpContext.Response.Redirect(href);
                    return;
                }

                var proxyManager = decryptLink?.plugin == "posterapi" ? new ProxyManager("posterapi", AppInit.conf.posterApi) : new ProxyManager("proxyimg", init);
                var proxy = proxyManager.Get();

                if (width == 0 && height == 0 && !cacheimg)
                {
                    #region bypass
                    bypass_reset:  var handler = CORE.HttpClient.Handler(href, proxy);
                    handler.AllowAutoRedirect = true;

                    using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : _httpClientFactory.CreateClient("base"))
                    {
                        CORE.HttpClient.DefaultRequestHeaders(client, 8, 0, null, null, decryptLink?.headers);

                        if (!handler.UseProxy)
                            client.DefaultRequestHeaders.ConnectionClose = false;

                        using (HttpResponseMessage response = await client.GetAsync(href).ConfigureAwait(false))
                        {
                            if (url_reserve != null && response.StatusCode != HttpStatusCode.OK)
                            {
                                href = url_reserve;
                                url_reserve = null;
                                goto bypass_reset;
                            }

                            httpContext.Response.StatusCode = (int)response.StatusCode;
                            httpContext.Response.Headers.Add("X-Cache-Status", "bypass");

                            if (response.Headers.TryGetValues("Content-Type", out var contype))
                                httpContext.Response.ContentType = contype?.FirstOrDefault() ?? contentType;

                            await response.Content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
                            return;
                        }
                    }
                    #endregion
                }
                else
                {
                    #region rsize / cache
                    rsize_reset: var array = await Download(href, proxy: proxy, headers: decryptLink?.headers);
                    if (array == null)
                    {
                        if (url_reserve != null)
                        {
                            href = url_reserve;
                            url_reserve = null;
                            goto rsize_reset;
                        }

                        if (cacheimg)
                            memoryCache.Set(memKeyErrorDownload, 0, DateTime.Now.AddSeconds(20));

                        proxyManager.Refresh();
                        httpContext.Response.Redirect(href);
                        return;
                    }

                    if (array.Length > 1000)
                    {
                        if (width > 0 || height > 0)
                        {
                            if (AppInit.conf.imagelibrary == "NetVips")
                            {
                                array = NetVipsImage(href, array, width, height);
                            }
                            else if (AppInit.conf.imagelibrary == "ImageMagick" && RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                            {
                                if (cacheimg)
                                    Directory.CreateDirectory($"cache/img/{md5key.Substring(0, 2)}");

                                array = ImageMagick(array, width, height, cacheimg ? outFile : null);
                            }
                        }
                    }

                    proxyManager.Success();
                    httpContext.Response.ContentType = contentType;
                    httpContext.Response.Headers.Add("X-Cache-Status", "MISS");
                    await httpContext.Response.Body.WriteAsync(array, httpContext.RequestAborted).ConfigureAwait(false);

                    if (array.Length > 1000 && cacheimg)
                    {
                        try
                        {
                            if (!File.Exists(outFile))
                            {
                                Directory.CreateDirectory($"cache/img/{md5key.Substring(0, 2)}");

                                using (var fileStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                                    await fileStream.WriteAsync(array, 0, array.Length).ConfigureAwait(false);
                            }
                        }
                        catch { try { File.Delete(outFile); } catch { } }
                    }
                    #endregion
                }
            }
            else
            {
                await _next(httpContext);
            }
        }


        #region Download
        async public ValueTask<byte[]> Download(string url, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = CORE.HttpClient.Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : _httpClientFactory.CreateClient("base"))
                {
                    CORE.HttpClient.DefaultRequestHeaders(client, 8, 0, null, null, headers);

                    if (!handler.UseProxy)
                        client.DefaultRequestHeaders.ConnectionClose = false;

                    using (HttpResponseMessage response = await client.GetAsync(url))
                    {
                        if (response.StatusCode != HttpStatusCode.OK)
                            return null;

                        using (HttpContent content = response.Content)
                        {
                            byte[] res = await content.ReadAsByteArrayAsync().ConfigureAwait(false);
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
                    if (image.Width > width || image.Height > height)
                    {
                        using (var res = image.ThumbnailImage(width == 0 ? image.Width : width, height == 0 ? image.Height : height, crop: NetVips.Enums.Interesting.None))
                        {
                            var buffer = href.Contains(".png") ? res.PngsaveBuffer() : res.JpegsaveBuffer();
                            if (buffer != null && buffer.Length > 1000)
                                return buffer;
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
        private byte[] ImageMagick(byte[] array, int width, int height, string myoutputFilePath)
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
