using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System.IO;
using System;
using System.Text;
using System.Security.Cryptography;
using ImageMagick;

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
            using (var md5 = MD5.Create())
            {
                var result = md5.ComputeHash(Encoding.UTF8.GetBytes(href));
                string md5key = BitConverter.ToString(result).Replace("-", "").ToLower();

                Directory.CreateDirectory($"cache/img/{md5key[0]}");
                return $"cache/img/{md5key[0]}/{md5key}";
            }
        }
        #endregion

        async public Task InvokeAsync(HttpContext httpContext)
        {
            if (httpContext.Request.Path.Value.StartsWith("/proxyimg/"))
            {
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

                var array = await CORE.HttpClient.Download(href, timeoutSeconds: 8);
                if (array == null)
                {
                    httpContext.Response.Redirect(href);
                    return;
                }

                if (href.Contains(".webp"))
                {
                    using (MagickImage image = new MagickImage(array))
                    {
                        image.Format = MagickFormat.Jpg;
                        array = image.ToByteArray();
                    }
                }

                if (!href.Contains("tmdb.org"))
                {
                    using (MagickImage image = new MagickImage(array))
                    {
                        if (image.Height > 200)
                        {
                            image.Resize(0, 200);
                            array = image.ToByteArray();
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
