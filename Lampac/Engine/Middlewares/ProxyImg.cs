﻿using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.CORE;
using Shared.Model.Online;
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
using System.Threading.Tasks;

namespace Lampac.Engine.Middlewares
{
    public class ProxyImg
    {
        #region ProxyImg
        static FileSystemWatcher fileWatcher;

        static ConcurrentDictionary<string, byte> cacheFiles = new ConcurrentDictionary<string, byte>();

        static ProxyImg()
        {
            Directory.CreateDirectory("cache/img");

            foreach (var item in Directory.GetFiles("cache/img", "*"))
                cacheFiles.TryAdd(Path.GetFileName(item), 0);

            fileWatcher = new FileSystemWatcher
            {
                Path = "cache/img",
                NotifyFilter = NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            fileWatcher.Created += (s, e) => { cacheFiles.TryAdd(e.Name, 0); };
            fileWatcher.Deleted += (s, e) => { cacheFiles.TryRemove(e.Name, out _); };
        }

        public ProxyImg(RequestDelegate next) { }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache)
        {
            var requestInfo = httpContext.Features.Get<RequestModel>();

            var init = AppInit.conf.serverproxy.image;
            bool cacheimg = init.cache && AppInit.conf.mikrotik == false;

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

            if (cacheimg && cacheFiles.ContainsKey(md5key))
            {
                httpContext.Response.Headers.Add("X-Cache-Status", "HIT");
                httpContext.Response.ContentType = contentType;
                await httpContext.Response.SendFileAsync(outFile).ConfigureAwait(false);
                return;
            }

            string memKeyErrorDownload = $"ProxyImg:ErrorDownload:{href}";
            if (memoryCache.TryGetValue(memKeyErrorDownload, out _))
            {
                httpContext.Response.Redirect(href);
                return;
            }

            httpContext.Response.Headers.Add("X-Cache-Status", cacheimg ? "MISS" : "bypass");

            var proxyManager = decryptLink?.plugin == "posterapi" ? new ProxyManager("posterapi", AppInit.conf.posterApi) : new ProxyManager("proxyimg", init);
            var proxy = proxyManager.Get();

            if (width == 0 && height == 0)
            {
                #region bypass
                bypass_reset: var handler = CORE.HttpClient.Handler(href, proxy);
                handler.AllowAutoRedirect = true;

                var client = FrendlyHttp.CreateClient("proxyimg", handler, "base", decryptLink?.headers?.ToDictionary(), updateClient: uclient =>
                {
                    CORE.HttpClient.DefaultRequestHeaders(uclient, 8, 0, null, null, decryptLink?.headers);
                });

                using (HttpResponseMessage response = await client.GetAsync(href).ConfigureAwait(false))
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
                            bool saveCache = true;

                            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);

                                try
                                {
                                    int bytesRead;
                                    Memory<byte> memoryBuffer = buffer.AsMemory();

                                    while ((bytesRead = await responseStream.ReadAsync(memoryBuffer, httpContext.RequestAborted).ConfigureAwait(false)) > 0)
                                    {
                                        memoryStream.Write(memoryBuffer.Slice(0, bytesRead).Span);
                                        await httpContext.Response.Body.WriteAsync(memoryBuffer.Slice(0, bytesRead), httpContext.RequestAborted).ConfigureAwait(false);
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
                                    if (!cacheFiles.ContainsKey(md5key))
                                        File.WriteAllBytes(outFile, memoryStream.ToArray());
                                }
                                catch { try { File.Delete(outFile); } catch { } }
                            }
                        }
                    }
                    else
                    {
                        await response.Content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted).ConfigureAwait(false);
                    }
                }
                #endregion
            }
            else
            {
                #region rsize
                rsize_reset: var array = await Download(href, proxy: proxy, headers: decryptLink?.headers).ConfigureAwait(false);
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

                proxyManager.Success();
                httpContext.Response.ContentType = contentType;
                await httpContext.Response.Body.WriteAsync(array, httpContext.RequestAborted).ConfigureAwait(false);

                if (array.Length > 1000 && cacheimg)
                {
                    try
                    {
                        if (!cacheFiles.ContainsKey(md5key))
                            File.WriteAllBytes(outFile, array);
                    }
                    catch { try { File.Delete(outFile); } catch { } }
                }
                #endregion
            }
        }


        #region Download
        async Task<byte[]> Download(string url, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = CORE.HttpClient.Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                var client = FrendlyHttp.CreateClient("proxyimg", handler, "base", headers?.ToDictionary(), updateClient: uclient => 
                {
                    CORE.HttpClient.DefaultRequestHeaders(uclient, 8, 0, null, null, headers);
                });

                using (HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false))
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
            catch
            {
                return null;
            }
        }
        #endregion

        #region NetVipsImage
        private byte[] NetVipsImage(string href, in byte[] array, int width, int height)
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
        static byte[] ImageMagick(in byte[] array, int width, int height, string myoutputFilePath)
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
