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
                Content(string.Empty, contentType: "application/javascript; charset=utf-8");

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
            string query = Regex.Replace(HttpContext.Request.QueryString.Value, "(&|\\?)(account_email|email|uid|token)=[^&]+", "");
            string uri = Regex.Match(path, "^[^/]+/(.*)").Groups[1].Value + query;

            if (!init.enable)
                return Redirect($"https://{path}/{query}");

            if (path.Split(".")[0] is "geo" or "tmdb" or "tmapi" or "apitmdb" or "imagetmdb" or "cdn" or "ad" or "ws")
                domain = $"{path.Split(".")[0]}.{domain}";

            if (domain == "geo")
                return Content(requestInfo.Country);

            if (domain is "ws" or "ad")
                return StatusCode(403); // не уметь

            if (path.StartsWith("api/checker") || uri.StartsWith("api/checker"))
                return Content("ok");

            if (uri.StartsWith("api/plugins/blacklist"))
                return ContentTo("[]");

            var headers = HeadersModel.Init();
            foreach (var header in HttpContext.Request.Headers)
            {
                if (header.Key.ToLower() is "cookie" or "token" or "profile" or "user-agent")
                    headers.Add(new HeadersModel(header.Key, header.Value.ToString()));
            }

            var proxyManager = new ProxyManager("cub_api", init);
            var result = await HttpClient.BaseDownload($"{init.scheme}://{domain}/{uri}", timeoutSeconds: 10, proxy: proxyManager.Get(), headers: headers, statusCodeOK: false, useDefaultHeaders: false);
            if (result.array == null || result.array.Length == 0)
            {
                proxyManager.Refresh();
                return StatusCode(result.response != null ? (int)result.response.StatusCode : 502);
            }

            HttpContext.Response.StatusCode = (int)result.response.StatusCode;
            return File(result.array, result.response.Content.Headers.ContentType.ToString());
        }
    }
}