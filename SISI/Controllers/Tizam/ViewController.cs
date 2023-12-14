using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
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
            if (memoryCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("tizam", init);
            var proxy = proxyManager.Get();

            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                string html = await HttpClient.Get($"{init.corsHost()}/{uri}", timeoutSeconds: 10, proxy: proxy);
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
                memoryCache.Set(memKey, location, cacheTime(360));
            }

            return Redirect(HostStreamProxy(init, location, proxy: proxy));
        }
    }
}
