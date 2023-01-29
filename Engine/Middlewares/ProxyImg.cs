using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using Lampac.Engine.CORE;
using NetVips;
using System.Text.RegularExpressions;
using System.Collections.Generic;

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

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxyimg"))
            {
                if (!AppInit.conf.serverproxy.enable || (!AppInit.conf.serverproxy.allow_tmdb && httpContext.Request.Path.Value.Contains(".tmdb.org")))
                {
                    httpContext.Response.StatusCode = 403;
                    return;
                }

                if (HttpMethods.IsOptions(httpContext.Request.Method))
                {
                    httpContext.Response.StatusCode = 405;
                    return;
                }

                string href = Regex.Replace(httpContext.Request.Path.Value, "/proxyimg([^/]+)?/", "") + httpContext.Request.QueryString.Value;
                href = Regex.Replace(href, "(\\?|&)account_email=([^&]+)", "", RegexOptions.IgnoreCase);

                if (!href.Contains("image.tmdb.org"))
                    href = ProxyLink.Decrypt(href);

                if (string.IsNullOrWhiteSpace(href))
                {
                    httpContext.Response.StatusCode = 404;
                    return;
                }

                string outFile = getFolder(href);

                if (File.Exists(outFile))
                {
                    httpContext.Response.ContentType = "image/jpeg";
                    httpContext.Response.Headers.Add("X-Cache-Status", "HIT");

                    using (var fs = new FileStream(outFile, FileMode.Open))
                    {
                        await fs.CopyToAsync(httpContext.Response.Body);
                    }

                    return;
                }

                List<(string name, string val)> headers = new List<(string name, string val)>();

                if (href.Contains("cdntrex."))
                    headers.Add(("referer", AppInit.conf.Porntrex.host));

                var array = await HttpClient.Download(href, timeoutSeconds: 8, useproxy: AppInit.conf.serverproxy.useproxy, addHeaders: headers);
                if (array == null)
                {
                    httpContext.Response.Redirect(href);
                    return;
                }

                if (httpContext.Request.Path.Value.StartsWith("/proxyimg:"))
                {
                    var gimg = Regex.Match(httpContext.Request.Path.Value, "/proxyimg:([0-9]+):([0-9]+)").Groups;

                    int width = int.Parse(gimg[1].Value);
                    int height = int.Parse(gimg[2].Value);

                    using (var image = Image.NewFromBuffer(array))
                    {
                        if (image.Width > width || image.Height > height)
                        {
                            using (var res = image.ThumbnailImage(width == 0 ? image.Width : width, height == 0 ? image.Height : height, crop: Enums.Interesting.None))
                                array = res.JpegsaveBuffer();
                        }
                    }
                }

                await File.WriteAllBytesAsync(outFile, array);

                httpContext.Response.ContentType = "image/jpeg";
                httpContext.Response.Headers.Add("X-Cache-Status", "MISS");

                await httpContext.Response.Body.WriteAsync(array);
            }
            else
            {
                await _next(httpContext);
            }
        }
    }
}
