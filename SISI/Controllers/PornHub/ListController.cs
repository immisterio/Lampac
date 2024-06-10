using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using Shared.Model.Online;

namespace Lampac.Controllers.PornHub
{
    public class ListController : BaseSisiController
    {
        #region httpheaders
        public static List<HeadersModel> defaultheaders(string cookie = null)
        {
            return HeadersModel.Init(
                ("accept-language", "ru-RU,ru;q=0.9"),
                ("sec-ch-ua", "\"Chromium\";v=\"94\", \"Google Chrome\";v=\"94\", \";Not A Brand\";v=\"99\""),
                ("sec-ch-ua-mobile", "?0"),
                ("sec-ch-ua-platform", "\"Windows\""),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-dest", "document"),
                ("sec-fetch-site", "none"),
                ("sec-fetch-user", "?1"),
                ("upgrade-insecure-requests", "1"),
                ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/94.0.4606.81 Safari/537.36"),
                ("cookie", cookie ?? "platform=pc; bs=ukbqk2g03joiqzu68gitadhx5bhkm48j; ss=250837987735652383; fg_0d2ec4cbd943df07ec161982a603817e=56239.100000; atatusScript=hide; _gid=GA1.2.309162272.1686472069; d_fs=1; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uid=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; d_uidb=2f5e522a-fa28-a0fe-0ab2-fd90f45d96c0; accessAgeDisclaimerPH=1; cookiesBannerSeen=1; _gat=1; __s=64858645-42FE722901BBA6E6-125476E1; __l=64858645-42FE722901BBA6E6-125476E1; hasVisited=1; fg_f916a4d27adf4fc066cd2d778b4d388e=78731.100000; fg_fa3f0973fd973fca3dfabc86790b408b=12606.100000; _ga_B39RFFWGYY=GS1.1.1686472069.1.1.1686472268.0.0.0; _ga=GA1.1.1515398043.1686472069")
            );
        }
        #endregion


        [HttpGet]
        [Route("phub")]
        [Route("phubgay")]
        [Route("phubsml")]
        async public Task<JsonResult> Index(string search, string model, string sort, int c, int pg = 1)
        {
            var init = AppInit.conf.PornHub;

            if (!init.enable)
                return OnError("disable");

            string plugin = Regex.Match(HttpContext.Request.Path.Value, "^/([a-z]+)").Groups[1].Value;

            string memKey = $"{plugin}:list:{search}:{model}:{sort}:{c}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (int total_pages, List<PlaylistItem> playlists) cache))
            {
                var proxyManager = new ProxyManager("phub", init);
                var proxy = proxyManager.Get();

                string html = await PornHubTo.InvokeHtml(init.corsHost(), plugin, search, model, sort, c, null, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, httpversion: 2, headers: httpHeaders(init, defaultheaders())));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                cache.total_pages = PornHubTo.Pages(html);
                cache.playlists = PornHubTo.Playlist($"{host}/phub/vidosik", "phub", html, IsModel_page: !string.IsNullOrEmpty(model));

                if (cache.playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(10, init: init));
            }

            return OnResult(cache.playlists, string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, plugin, search, sort, c) : null, plugin: "phub", total_pages: cache.total_pages);
        }


        [HttpGet]
        [Route("phubprem")]
        async public Task<JsonResult> Prem(string search, string model, string sort, string hd, int c, int pg = 1)
        {
            var init = AppInit.conf.PornHubPremium;

            if (!init.enable)
                return OnError("disable");

            string memKey = $"phubprem:list:{search}:{model}:{sort}:{hd}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out (int total_pages, List<PlaylistItem> playlists) cache))
            {
                var proxyManager = new ProxyManager("phubprem", init);
                var proxy = proxyManager.Get();

                string html = await PornHubTo.InvokeHtml(init.corsHost(), "phubprem", search, model, sort, c, hd, pg, url => HttpClient.Get(init.cors(url), timeoutSeconds: 14, proxy: proxy, httpversion: 2, headers: httpHeaders(init, defaultheaders(init.cookie))));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                cache.total_pages = PornHubTo.Pages(html);
                cache.playlists = PornHubTo.Playlist($"{host}/phubprem/vidosik", "phubprem", html, prem: true);

                if (cache.playlists.Count == 0)
                    return OnError("playlists", proxyManager, pg > 1 && string.IsNullOrEmpty(search));

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(10, init: init));
            }

            return OnResult(cache.playlists, string.IsNullOrEmpty(model) ? PornHubTo.Menu(host, "phubprem", search, sort, c, hd) : null, plugin: "phubprem", total_pages: cache.total_pages);
        }
    }
}
