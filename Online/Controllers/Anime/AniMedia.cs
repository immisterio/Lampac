using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class AniMedia : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.AniMedia);

        [HttpGet]
        [Route("lite/animedia")]
        async public ValueTask<ActionResult> Index(string title, string news, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.AniMedia);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(news))
            {
                if (string.IsNullOrEmpty(title))
                    return OnError();

                #region Поиск
                string memkey = $"animedia:search:{title}:{similar}";

                return await InvkSemaphore(init, memkey, async () =>
                {
                    if (!hybridCache.TryGetValue(memkey, out List<(string title, string url, string img)> catalog, inmemory: false))
                    {
                        string search = await Http.Post($"{init.corsHost()}/index.php?do=search", $"do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                        if (search == null)
                            return OnError(proxyManager);

                        var rows = search.Split("</article>")[1].Split("grid-item d-flex fd-column");

                        catalog = new List<(string title, string url, string img)>(rows.Length);

                        foreach (string row in rows.Skip(1))
                        {
                            var g = Regex.Match(row, "<a href=\"https?://[^/]+/([^\"]+)\" class=\"poster__link\"><h3 class=\"poster__title line-clamp\">([^<]+)</h3></a>").Groups;

                            if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(g[2].Value))
                            {
                                string img = Regex.Match(row, "<img src=\"([^\"]+)\"").Groups[1].Value;
                                if (!string.IsNullOrEmpty(img))
                                    img = init.host + img;

                                if (similar || StringConvert.SearchName(g[2].Value).Contains(StringConvert.SearchName(title)))
                                    catalog.Add((g[2].Value, g[1].Value, img));
                            }
                        }

                        if (catalog.Count == 0 && !search.Contains("id=\"dosearch\""))
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memkey, catalog, cacheTime(40, init: init), inmemory: false);
                    }

                    if (catalog.Count == 0)
                        return OnError();

                    if (!similar && catalog.Count == 1)
                        return LocalRedirect(accsArgs($"/lite/animedia?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&news={HttpUtility.UrlEncode(catalog[0].url)}"));

                    var stpl = new SimilarTpl(catalog.Count);

                    foreach (var res in catalog)
                    {
                        string uri = $"{host}/lite/animedia?title={HttpUtility.UrlEncode(title)}&news={HttpUtility.UrlEncode(res.url)}";
                        stpl.Append(res.title, string.Empty, string.Empty, uri, PosterApi.Size(res.img));
                    }

                    return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                });
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animedia:{news}";

                return await InvkSemaphore(init, memKey, async () =>
                {
                    if (!hybridCache.TryGetValue(memKey, out List<(int episode, string s, string vod)> links, inmemory: false))
                    {
                        string html = await Http.Get($"{init.corsHost()}/{news}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                        if (html == null)
                            return OnError(proxyManager);

                        var match = Regex.Match(html, "data-vid=\"([0-9]+)\"[\t ]+data-vlnk=\"([^\"]+)\"");
                        links = new List<(int episode, string s, string vod)>(match.Length);

                        string pmovie = Regex.Match(html, "class=\"pmovie__main-info ws-nowrap\">([^<]+)<").Groups[1].Value;
                        string s = Regex.Match(pmovie, "Season[\t ]+([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value;
                        if (string.IsNullOrEmpty(s))
                            s = "1";

                        while (match.Success)
                        {
                            string vod = match.Groups[2].Value;
                            if (!string.IsNullOrEmpty(match.Groups[1].Value) && !string.IsNullOrEmpty(vod) && vod.Contains("/vod/"))
                            {
                                if (int.TryParse(match.Groups[1].Value, out int episode) && episode > 0)
                                {
                                    if (links.FirstOrDefault(i => i.episode == episode).vod == null)
                                        links.Add((episode, s, vod));
                                }
                            }

                            match = match.NextMatch();
                        }

                        if (links.Count == 0)
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memKey, links, cacheTime(30, init: init), inmemory: false);
                    }

                    var etpl = new EpisodeTpl(links.Count);

                    foreach (var l in links.OrderBy(i => i.episode))
                        etpl.Append($"{l.episode} серия", title, l.s, l.episode.ToString(), accsArgs($"{host}/lite/animedia/video.m3u8?vod={HttpUtility.UrlEncode(l.vod)}"), vast: init.vast);

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                });
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animedia/video.m3u8")]
        async public ValueTask<ActionResult> Video(string vod)
        {
            var init = await loadKit(AppInit.conf.AniMedia);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            string memKey = $"animedia:{vod}";

            return await InvkSemaphore(init, memKey, async () =>
            {
                if (!hybridCache.TryGetValue(memKey, out string hls))
                {
                    string embed = await Http.Get(vod, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                    if (string.IsNullOrEmpty(embed))
                        return OnError(proxyManager);

                    hls = Regex.Match(embed, "file:\"([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(hls))
                        return OnError(proxyManager);

                    proxyManager.Success();
                    hybridCache.Set(memKey, hls, cacheTime(180, init: init));
                }

                return Redirect(HostStreamProxy(init, hls, proxy: proxyManager.Get()));
            });
        }
        #endregion
    }
}
