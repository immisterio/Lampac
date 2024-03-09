using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using Lampac.Engine.CORE;
using NetVips;
using System.Text.RegularExpressions;
using Shared.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Engine.Middlewares
{
    public class ProxyImg
    {
        #region ProxyImg
        private readonly RequestDelegate _next;

        public ProxyImg(RequestDelegate next)
        {
            _next = next;
        }
        #endregion

        #region getFolder
        static string getFolder(string href)
        {
            string md5key = CrypTo.md5(href);
            Directory.CreateDirectory($"cache/img/{md5key.Substring(0, 2)}");
            return $"cache/img/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext, IMemoryCache memoryCache)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxyimg"))
            {
                var init = AppInit.conf.serverproxy;

                #region Проверки
                Shared.Models.ProxyLinkModel decryptLink = null;
                string href = Regex.Replace(httpContext.Request.Path.Value, "/proxyimg([^/]+)?/", "") + httpContext.Request.QueryString.Value;

                if (init.encrypt)
                {
                    if (href.Contains(".tmdb.org"))
                    {
                        if (!init.allow_tmdb)
                        {
                            httpContext.Response.StatusCode = 403;
                            return;
                        }
                    }
                    else
                    {
                        decryptLink = ProxyLink.Decrypt(Regex.Replace(href, "(\\?|&).*", ""), httpContext.Connection.RemoteIpAddress.ToString());
                        href = decryptLink?.uri;
                    }
                }
                else
                {
                    if (!init.enable)
                    {
                        httpContext.Response.StatusCode = 403;
                        return;
                    }
                }

                if (string.IsNullOrWhiteSpace(href))
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
                    var gimg = Regex.Match(httpContext.Request.Path.Value, "/proxyimg:([0-9]+):([0-9]+)").Groups;
                    width = int.Parse(gimg[1].Value);
                    height = int.Parse(gimg[2].Value);
                }
                #endregion

                string outFile = getFolder($"{href}:{width}:{height}");

                if (init.cache.img && File.Exists(outFile))
                {
                    httpContext.Response.ContentType = "image/jpeg";
                    httpContext.Response.Headers.Add("X-Cache-Status", "HIT");

                    using (var fs = new FileStream(outFile, FileMode.Open, FileAccess.Read))
                        await fs.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);

                    return;
                }

                string memKeyErrorDownload = $"ProxyImg:ErrorDownload:{href}";
                if (memoryCache.TryGetValue(memKeyErrorDownload, out _))
                {
                    httpContext.Response.Redirect(href);
                    return;
                }

                var proxyManager = new ProxyManager("proxyimg", AppInit.conf.serverproxy);

                var array = await HttpClient.Download(href, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: decryptLink?.headers);
                if (array == null)
                {
                    if (init.cache.img)
                        memoryCache.Set(memKeyErrorDownload, 0, DateTime.Now.AddMinutes(2));

                    proxyManager.Refresh();
                    httpContext.Response.Redirect(href);
                    return;
                }

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

                if (init.cache.img && !File.Exists(outFile))
                {
                    try
                    {
                        await File.WriteAllBytesAsync(outFile, array);
                    }
                    catch { try { File.Delete(outFile); } catch { } }
                }

                httpContext.Response.ContentType = "image/jpeg";
                httpContext.Response.Headers.Add("X-Cache-Status", init.cache.img ? "MISS" : "bypass");

                await httpContext.Response.Body.WriteAsync(array, httpContext.RequestAborted);
            }
            else
            {
                await _next(httpContext);
            }
        }
    }
}
