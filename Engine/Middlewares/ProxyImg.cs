using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using Lampac.Engine.CORE;
using NetVips;

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
            Directory.CreateDirectory($"cache/img/{md5key[0]}");
            return $"cache/img/{md5key[0]}/{md5key}";
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxyimg/"))
            {
                if (AppInit.conf.disableserverproxy)
                {
                    httpContext.Response.StatusCode = 403;
                    return;
                }

                if (HttpMethods.IsOptions(httpContext.Request.Method))
                {
                    httpContext.Response.StatusCode = 405;
                    return;
                }

                string href = httpContext.Request.Path.Value.Replace("/proxyimg/", "") + httpContext.Request.QueryString.Value;
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

                var array = await HttpClient.Download(href, timeoutSeconds: 8, useproxy: AppInit.conf.proxytoproxyimg);
                if (array == null)
                {
                    httpContext.Response.Redirect(href);
                    return;
                }

                if (!href.Contains("tmdb.org"))
                {
                    using (var image = Image.NewFromBuffer(array))
                    {
                        if (image.Height > 200)
                        {
                            using (var res = image.ThumbnailImage(image.Width, 200, crop: Enums.Interesting.None))
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
