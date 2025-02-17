using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using Lampac.Engine.CORE;
using NetVips;
using System.Text.RegularExpressions;
using Shared.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net.Http;
using Shared.Model.Online;
using System.Collections.Generic;
using System.Net;
using Shared.Models;

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

                var init = AppInit.conf.serverproxy;
                bool cacheimg = init.cache.img;

                #region Проверки
                string href = Regex.Replace(httpContext.Request.Path.Value, "/proxyimg([^/]+)?/", "").Replace("://", ":/_/").Replace("//", "/").Replace(":/_/", "://") + httpContext.Request.QueryString.Value;
                ProxyLinkModel decryptLink = ProxyLink.Decrypt(Regex.Replace(href, "(\\?|&).*", ""), requestInfo.IP);

                if (href.Contains("image.tmdb.org"))
                {
                    httpContext.Response.Redirect($"/tmdb/img/{Regex.Replace(href, "^https?://[^/]+/", "")}");
                    return;
                }

                if (init.encrypt)
                {
                    href = decryptLink?.uri;
                }
                else
                {
                    if (!init.enable)
                    {
                        httpContext.Response.StatusCode = 403;
                        return;
                    }

                    if (decryptLink?.uri != null)
                        href = decryptLink.uri;
                }

                if (string.IsNullOrWhiteSpace(href) || !href.StartsWith("http"))
                {
                    httpContext.Response.StatusCode = 404;
                    return;
                }
                #endregion

                #region width / height
                int width = 0;
                int height = 0;

                if (httpContext.Request.Path.Value.StartsWith("/proxyimg:"))
                {
                    if (!cacheimg)
                        cacheimg = init.cache.img_rsize;

                    var gimg = Regex.Match(httpContext.Request.Path.Value, "/proxyimg:([0-9]+):([0-9]+)").Groups;
                    width = int.Parse(gimg[1].Value);
                    height = int.Parse(gimg[2].Value);
                }
                #endregion

                string md5key = CrypTo.md5($"{href}:{width}:{height}");
                string outFile = $"cache/img/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";

                if (cacheimg && File.Exists(outFile))
                {
                    httpContext.Response.ContentType = "image/jpeg";
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

                var proxyManager = new ProxyManager("proxyimg", AppInit.conf.serverproxy);
                var proxy = proxyManager.Get();

                var array = await Download(href, proxy: proxy, headers: decryptLink?.headers);
                if (array == null)
                {
                    if (cacheimg)
                        memoryCache.Set(memKeyErrorDownload, 0, DateTime.Now.AddMinutes(1));

                    proxyManager.Refresh();
                    httpContext.Response.Redirect(href);
                    return;
                }

                if (array.Length > 1000)
                {
                    if (width > 0 || height > 0)
                    {
                        using (var image = Image.NewFromBuffer(array))
                        {
                            if (image.Width > width || image.Height > height)
                            {
                                using (var res = image.ThumbnailImage(width == 0 ? image.Width : width, height == 0 ? image.Height : height, crop: Enums.Interesting.None))
                                    array = res.JpegsaveBuffer();
                            }
                        }
                    }

                    if (array != null && cacheimg)
                    {
                        try
                        {
                            Directory.CreateDirectory($"cache/img/{md5key.Substring(0, 2)}");
                            await File.WriteAllBytesAsync(outFile, array).ConfigureAwait(false);
                        }
                        catch { try { File.Delete(outFile); } catch { } }
                    }

                    httpContext.Response.ContentType = "image/jpeg";
                    httpContext.Response.Headers.Add("X-Cache-Status", cacheimg ? "MISS" : "bypass");
                }

                await httpContext.Response.Body.WriteAsync(array, httpContext.RequestAborted).ConfigureAwait(false);
            }
            else
            {
                await _next(httpContext);
            }
        }


        async public ValueTask<byte[]> Download(string url, List<HeadersModel> headers = null, WebProxy proxy = null)
        {
            try
            {
                var handler = CORE.HttpClient.Handler(url, proxy);
                handler.AllowAutoRedirect = true;

                using (var client = handler.UseProxy ? new System.Net.Http.HttpClient(handler) : _httpClientFactory.CreateClient("base"))
                {
                    CORE.HttpClient.DefaultRequestHeaders(client, 8, 0, null, null, headers);
                    client.DefaultRequestHeaders.ConnectionClose = false;

                    using (HttpResponseMessage response = await client.GetAsync(url))
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
    }
}
