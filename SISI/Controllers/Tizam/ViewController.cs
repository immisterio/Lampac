using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using System.Text.RegularExpressions;
using SISI;
using Shared.Engine.CORE;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.Tizam
{
    public class ViewController : BaseSisiController
    {
        [Route("tizam/vidosik")]
        async public Task<ActionResult> Index(string uri)
        {
            var init = AppInit.conf.Tizam;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"tizam:view:{uri}";
            if (hybridCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("tizam", init);
            var proxy = proxyManager.Get();

            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                string html = await HttpClient.Get($"{init.corsHost()}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));
                if (html == null)
                    return OnError("html", proxyManager);

                location = Regex.Match(html, "class=\"tab-video-2\">[^<>]+ src=\"https?://[^/]+/videoapi/directplayer\\.html\\?url=(http[^\"]+\\.mp4)\"").Groups[1].Value;

                if (string.IsNullOrEmpty(location))
                    location = Regex.Match(html, "class=\"tab-video-1\">[^<>]+ src=\"https?://[^/]+/videoapi/directplayer\\.html\\?url=(http[^\"]+\\.mp4)\"").Groups[1].Value;

                if (string.IsNullOrEmpty(location))
                    location = Regex.Match(html, "src=\"https?://[^/]+/videoapi/directplayer\\.html\\?url=(http[^\"]+\\.mp4)\"").Groups[1].Value;

                if (string.IsNullOrEmpty(location))
                    return OnError("location", proxyManager);

                location = HttpUtility.UrlDecode(location);

                proxyManager.Success();
                hybridCache.Set(memKey, location, cacheTime(360, init: init));
            }

            return Redirect(HostStreamProxy(init, location, proxy: proxy));
        }
    }
}
