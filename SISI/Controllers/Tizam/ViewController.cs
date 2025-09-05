using Microsoft.AspNetCore.Mvc;

namespace SISI.Controllers.Tizam
{
    public class ViewController : BaseSisiController
    {
        [Route("tizam/vidosik")]
        async public ValueTask<ActionResult> Index(string uri)
        {
            var init = await loadKit(AppInit.conf.Tizam);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return OnError(rch_error);

            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            string memKey = $"tizam:view:{uri}";

            return await InvkSemaphore(memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out StreamItem stream_links))
                {
                    reset:
                    string html = rch.enable ? await rch.Get($"{init.corsHost()}/{uri}", httpHeaders(init)) :
                                               await Http.Get($"{init.corsHost()}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));

                    string location = Regex.Match(html ?? string.Empty, "src=\"(https?://[^\"]+\\.mp4)\" type=\"video/mp4\"").Groups[1].Value;

                    if (string.IsNullOrEmpty(location))
                    {
                        if (IsRhubFallback(init))
                            goto reset;

                        return OnError("location", proxyManager);
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    stream_links = new StreamItem()
                    {
                        qualitys = new Dictionary<string, string>()
                        {
                            ["auto"] = location
                        }
                    };

                    hybridCache.Set(memKey, stream_links, cacheTime(180, init: init));
                }

                return OnResult(stream_links, init, proxy);
            });
        }
    }
}
