using System.Collections.Generic;
using System.Threading.Tasks;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Model.Online;
using SISI;

namespace Lampac.Controllers.BongaCams
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("bgs")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = AppInit.conf.BongaCams;

            if (!init.enable)
                return OnError("disable");

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (!string.IsNullOrEmpty(search))
                return OnError("no search");

            var proxyManager = new ProxyManager("bgs", init);
            var proxy = proxyManager.Get();

            string memKey = $"BongaCams:list:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (List<PlaylistItem> playlists, int total_pages) cache))
            {
                string html = await BongaCamsTo.InvokeHtml(init.corsHost(), sort, pg, url => 
                {
                    return HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, httpversion: 2, headers: httpHeaders(init, HeadersModel.Init(
                        ("dnt", "1"),
                        ("cache-control", "no-cache"),
                        ("pragma", "no-cache"),
                        ("priority", "u=1, i"),
                        ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\""),
                        ("sec-ch-ua-mobile", "?0"),
                        ("sec-ch-ua-platform", "\"Windows\""),
                        ("referer", init.host),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    )));
                });

                if (html == null)
                    return OnError("html", proxyManager);

                cache.playlists = BongaCamsTo.Playlist(html, out int total_pages);
                cache.total_pages = total_pages;

                if (cache.playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1);

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(5, init: init));
            }

            return OnResult(cache.playlists, init, BongaCamsTo.Menu(host, sort), proxy: proxy, plugin: "bgs", total_pages: cache.total_pages);
        }
    }
}
