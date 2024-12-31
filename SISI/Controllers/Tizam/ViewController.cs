using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
            var init = AppInit.conf.Tizam.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            string memKey = $"tizam:view:{uri}";
            if (hybridCache.TryGetValue($"error:{memKey}", out string errormsg))
                return OnError(errormsg);

            var proxyManager = new ProxyManager("tizam", init);
            var proxy = proxyManager.Get();

            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error, false);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string html = rch.enable ? await rch.Get($"{init.corsHost()}/{uri}", httpHeaders(init)) : 
                                           await HttpClient.Get($"{init.corsHost()}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));
                
                location = Regex.Match(html ?? string.Empty, "src=\"(https?://[^\"]+\\.mp4)\" type=\"video/mp4\"").Groups[1].Value;

                if (string.IsNullOrEmpty(location))
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("location", proxyManager, !rch.enable);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, location, cacheTime(240, init: init));
            }

            return Redirect(HostStreamProxy(init, location, proxy: proxy));
        }
    }
}
