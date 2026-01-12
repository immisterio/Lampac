using Microsoft.AspNetCore.Mvc;
using Shared.Engine.RxEnumerate;

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
                    bool reqOk = false;
                    List<(string title, string url, string img)> catalog = null;

                    if (!hybridCache.TryGetValue(key, out catalog, inmemory: false))
                    {
                        await httpHydra.PostSpan($"{init.corsHost()}/index.php?do=search", $"do=search&subaction=search&from_page=0&story={HttpUtility.UrlEncode(title)}", search => 
                        {
                            reqOk = search.Contains("id=\"dosearch\"", StringComparison.Ordinal);

                            var article = Rx.Split("</article>", search);
                            if (article.Count > 1)
                            {
                                var rx = Rx.Split("grid-item d-flex fd-column", article[1].Span, 1);

                                catalog = new List<(string title, string url, string img)>(rx.Count);

                                foreach (var row in rx.Rows())
                                {
                                    var g = row.Groups("<a href=\"https?://[^/]+/([^\"]+)\" class=\"poster__link\"><h3 class=\"poster__title line-clamp\">([^<]+)</h3></a>");

                                    if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(g[2].Value))
                                    {
                                        string img = row.Match("<img src=\"([^\"]+)\"");
                                        if (img != null)
                                            img = init.host + img;

                                        if (similar || StringConvert.SearchName(g[2].Value).Contains(StringConvert.SearchName(title)))
                                            catalog.Add((g[2].Value, g[1].Value, img));
                                    }
                                }
                            }
                        });

                        if (catalog == null || catalog.Count == 0)
                            return OnError(refresh_proxy: !reqOk);

                        proxyManager?.Success();
                        hybridCache.Set(key, catalog, cacheTime(40), inmemory: false);
                    }

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
                    List<(int episode, string s, string vod)> links = null;

                    if (!hybridCache.TryGetValue(key, out links, inmemory: false))
                    {
                        await httpHydra.GetSpan($"{init.corsHost()}/{news}", html =>
                        {
                            var rx = Rx.Matches("data-vid=\"([0-9]+)\"[\t ]+data-vlnk=\"([^\"]+)\"", html);
                            if (rx.Count == 0)
                                return;

                            links = new List<(int episode, string s, string vod)>(rx.Count);

                            string pmovie = Rx.Match(html, "class=\"pmovie__main-info ws-nowrap\">([^<]+)<");
                            string s = Rx.Match(pmovie, "Season[\t ]+([0-9]+)", 1, RegexOptions.IgnoreCase);
                            if (string.IsNullOrEmpty(s))
                                s = "1";

                            foreach (var row in rx.Rows())
                            {
                                var g = row.Groups();

                                string vod = g[2].Value;
                                if (!string.IsNullOrEmpty(g[1].Value) && !string.IsNullOrEmpty(vod) && vod.Contains("/vod/"))
                                {
                                    if (int.TryParse(g[1].Value, out int episode) && episode > 0)
                                    {
                                        if (links.FirstOrDefault(i => i.episode == episode).vod == null)
                                            links.Add((episode, s, vod));
                                    }
                                }
                            }
                        });

                        if (links == null || links.Count == 0)
                            return OnError(refresh_proxy: true);

                        links = links.OrderBy(i => i.episode).ToList();

                        proxyManager?.Success();
                        hybridCache.Set(key, links, cacheTime(30), inmemory: false);
                    }

                    var etpl = new EpisodeTpl(links.Count);

                    foreach (var l in links)
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
                    await httpHydra.GetSpan(vod, embed => 
                    {
                        hls = Rx.Match(embed, "file:\"([^\"]+)\"");
                    });

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
