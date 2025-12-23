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

            return await SemaphoreResult($"tizam:view:{uri}", async e =>
            {
                reset:
                if (rch.enable == false)
                    await e.semaphore.WaitAsync();

                if (!hybridCache.TryGetValue(e.key, out StreamItem stream_links))
                {
                    string html = rch.enable
                        ? await rch.Get($"{init.corsHost()}/{uri}", httpHeaders(init))
                        : await Http.Get($"{init.corsHost()}/{uri}", timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));

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

                    hybridCache.Set(e.key, stream_links, cacheTime(180, init: init));
                }

                return OnResult(stream_links, init, proxy);
            });
        }
    }
}
