using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class AniMedia : BaseOnlineController
    {
        public AniMedia() : base(AppInit.conf.AniMedia) { }

        [HttpGet]
        [Route("lite/animedia")]
        async public Task<ActionResult> Index(string title, string news, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(news))
            {
                if (string.IsNullOrEmpty(title))
                    return OnError();

                #region Поиск
                return await InvkSemaphore($"animedia:search:{title}:{similar}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out List<(string title, string url, string img)> catalog, inmemory: false))
                    {
                        string search = await httpHydra.Post($"{init.corsHost()}/index.php?do=search", $"do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title)}");
                        if (search == null)
                            return OnError(refresh_proxy: true);

                        var rx = new RxEnumerate("grid-item d-flex fd-column", search.Split("</article>")[1], 1);

                        catalog = new List<(string title, string url, string img)>(rx.Count());

                        foreach (string row in rx.Rows())
                        {
                            var g = Regex.Match(row, "<a href=\"https?://[^/]+/([^\"]+)\" class=\"poster__link\"><h3 class=\"poster__title line-clamp\">([^<]+)</h3></a>").Groups;

                            if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(g[2].Value))
                            {
                                string img = Regex.Match(row, "<img src=\"([^\"]+)\"", RegexOptions.Compiled).Groups[1].Value;
                                if (!string.IsNullOrEmpty(img))
                                    img = init.host + img;

                                if (similar || StringConvert.SearchName(g[2].Value).Contains(StringConvert.SearchName(title)))
                                    catalog.Add((g[2].Value, g[1].Value, img));
                            }
                        }

                        if (catalog.Count == 0 && !search.Contains("id=\"dosearch\""))
                            return OnError();

                        proxyManager?.Success();
                        hybridCache.Set(key, catalog, cacheTime(40), inmemory: false);
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

                    return await ContentTpl(stpl);
                });
                #endregion
            }
            else 
            {
                #region Серии
                return await InvkSemaphore($"animedia:{news}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out List<(int episode, string s, string vod)> links, inmemory: false))
                    {
                        string html = await httpHydra.Get($"{init.corsHost()}/{news}");
                        if (html == null)
                            return OnError(refresh_proxy: true);

                        var match = Regex.Match(html, "data-vid=\"([0-9]+)\"[\t ]+data-vlnk=\"([^\"]+)\"", RegexOptions.Compiled);
                        links = new List<(int episode, string s, string vod)>(match.Length);

                        string pmovie = Regex.Match(html, "class=\"pmovie__main-info ws-nowrap\">([^<]+)<", RegexOptions.Compiled).Groups[1].Value;
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

                        proxyManager?.Success();
                        hybridCache.Set(key, links, cacheTime(30), inmemory: false);
                    }

                    var etpl = new EpisodeTpl(links.Count);

                    foreach (var l in links.OrderBy(i => i.episode))
                        etpl.Append($"{l.episode} серия", title, l.s, l.episode.ToString(), accsArgs($"{host}/lite/animedia/video.m3u8?vod={HttpUtility.UrlEncode(l.vod)}"), vast: init.vast);

                    return await ContentTpl(etpl);
                });
                #endregion
            }
        }

        #region Video
        [HttpGet]
        [Route("lite/animedia/video.m3u8")]
        async public ValueTask<ActionResult> Video(string vod)
        {
            if (await IsRequestBlocked(rch: false, rch_check: false))
                return badInitMsg;

            return await InvkSemaphore($"animedia:{vod}", async key =>
            {
                if (!hybridCache.TryGetValue(key, out string hls))
                {
                    string embed = await httpHydra.Get(vod);

                    if (string.IsNullOrEmpty(embed))
                        return OnError(refresh_proxy: true);

                    hls = Regex.Match(embed, "file:\"([^\"]+)\"", RegexOptions.Compiled).Groups[1].Value;
                    if (string.IsNullOrEmpty(hls))
                        return OnError(refresh_proxy: true);

                    proxyManager?.Success();
                    hybridCache.Set(key, hls, cacheTime(180));
                }

                return Redirect(HostStreamProxy(hls));
            });
        }
        #endregion
    }
}
