using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Shared.Engine;
using System.Web;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Microsoft.AspNetCore.Http;
using Shared.Model.Online;
using System.Text;

namespace Lampac.Controllers
{
    public class CubController : BaseController
    {
        #region cubproxy.js
        [HttpGet]
        [Route("cubproxy.js")]
        [Route("cubproxy/js/{token}")]
        public ActionResult CubProxy(string token)
        {
            if (!AppInit.conf.cub.enable)
                return Content(string.Empty, contentType: "application/javascript; charset=utf-8");

            string file = FileCache.ReadAllText("plugins/cubproxy.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("/cub/{*suffix}")]
        async public Task<ActionResult> Index()
        {
            var init = AppInit.conf.cub;
            string domain = init.domain;
            string path = HttpContext.Request.Path.Value.Replace("/cub/", "");
            string query = HttpContext.Request.QueryString.Value;
            string uri = Regex.Match(path, "^[^/]+/(.*)").Groups[1].Value + query;

            if (!init.enable || domain == "ws")
                return Redirect($"https://{path}/{query}");

            if (path.Split(".")[0] is "geo" or "tmdb" or "tmapi" or "apitmdb" or "imagetmdb" or "cdn" or "ad" or "ws")
                domain = $"{path.Split(".")[0]}.{domain}";

            if (domain.StartsWith("geo"))
                return Content(requestInfo.Country);

            if (path.StartsWith("api/checker") || uri.StartsWith("api/checker"))
                return Content("ok");

            if (uri.StartsWith("api/plugins/blacklist"))
                return ContentTo("[]");

            var proxyManager = new ProxyManager("cub_api", init);

            var headers = HeadersModel.Init();
            foreach (var header in HttpContext.Request.Headers)
            {
                if (header.Key.ToLower() is "cookie" or "token" or "profile" or "user-agent")
                    headers.Add(new HeadersModel(header.Key, header.Value.ToString()));
            }

            if (HttpMethods.IsPost(HttpContext.Request.Method))
            {
                string requestBody;
                using (var reader = new System.IO.StreamReader(HttpContext.Request.Body, Encoding.UTF8))
                    requestBody = await reader.ReadToEndAsync();

                string contentType = "application/x-www-form-urlencoded";
                if (!string.IsNullOrEmpty(HttpContext.Request.Headers.ContentType))
                    contentType = HttpContext.Request.Headers.ContentType.ToString().Split(";")[0];

                var streamContent = new System.Net.Http.StringContent(requestBody, Encoding.UTF8, contentType);
                var result = await HttpClient.BasePost($"{init.scheme}://{domain}/{uri}", streamContent, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers, statusCodeOK: false, useDefaultHeaders: false);
                if (result.content == null)
                {
                    proxyManager.Refresh();
                    return StatusCode((int)result.response.StatusCode);
                }

                if (result.response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                        HttpContext.Response.Headers.Append("Set-Cookie", cookie);
                }

                HttpContext.Response.StatusCode = (int)result.response.StatusCode;
                return Content(result.content, result.response.Content.Headers.ContentType.ToString());
            }
            else
            {
                reset: var result = await HttpClient.BaseDownload($"{init.scheme}://{domain}/{uri}", timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers, statusCodeOK: false, useDefaultHeaders: false);
                if (result.array == null || result.array.Length == 0)
                {
                    proxyManager.Refresh();
                    return StatusCode((int)result.response.StatusCode);
                }

                if ((domain.StartsWith("tmdb") || domain.StartsWith("tmapi") || domain.StartsWith("apitmdb")) && result.array.Length == 16)
                {
                    if (Encoding.UTF8.GetString(result.array) == "{\"blocked\":true}")
                    {
                        domain = "api.themoviedb.org";
                        goto reset;
                    }
                }

                if (result.response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                        HttpContext.Response.Headers.Append("Set-Cookie", cookie);
                }

                HttpContext.Response.StatusCode = (int)result.response.StatusCode;
                return File(result.array, result.response.Content.Headers.ContentType.ToString());
            }
        }
    }
}