using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.Kinobase;
using Microsoft.Playwright;
using System.Text.RegularExpressions;
using Shared.Engine;
using Shared.PlaywrightCore;

namespace Lampac.Controllers.LITE
{
    public class Kinobase : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinobase")]
        async public Task<ActionResult> Index(string title, int year, int s = -1, int serial = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Kinobase);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(title) || year == 0 || serial == 1)
                return OnError();

            if (PlaywrightBrowser.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(AppInit.conf.Kinobase);
            var proxy = proxyManager.BaseGet();

            var oninvk = new KinobaseInvoke
            (
               host,
               init.corsHost(),
               async ongettourl => 
               {
                   if (ongettourl.Contains("/search?query="))
                    return await HttpClient.Get(ongettourl, timeoutSeconds: 8, proxy: proxy.proxy, referer: init.host, httpversion: 2, headers: httpHeaders(init));

                   try
                   {
                       using (var browser = new PlaywrightBrowser())
                       {
                           var page = await browser.NewPageAsync(proxy: proxy.data);
                           if (page == null)
                               return null;

                           await page.AddInitScriptAsync("localStorage.setItem('pljsquality', '1080p');");

                           await page.RouteAsync("**/*", async route =>
                           {
                               if (!route.Request.Url.Contains(init.host) || route.Request.Url.Contains("/comments") || Regex.IsMatch(route.Request.Url, "\\.(jpe?g|png|css|svg|ico)"))
                               {
                                   await route.AbortAsync();
                                   return;
                               }

                               await route.ContinueAsync();
                           });

                           var result = await page.GotoAsync(ongettourl);
                           if (result == null)
                               return null;

                           await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                           string content = await page.ContentAsync();

                           PlaywrightBase.WebLog("GET", ongettourl, content, proxy.data);
                           return content;
                       }
                   }
                   catch
                   {
                       return null;
                   }
               },
               (url, data) => HttpClient.Post(url, data, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy.proxy),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<EmbedModel>($"kinobase:view:{title}:{year}:{proxyManager.CurrentProxyIp}", cacheTime(20, init: init), proxyManager, async res =>
            {
                return await oninvk.Embed(title, year);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, title, year, s, rjson));
        }
    }
}
