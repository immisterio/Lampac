using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class Animebesst : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.Animebesst);

        [HttpGet]
        [Route("lite/animebesst")]
        async public ValueTask<ActionResult> Index(string title, string uri, int s, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.Animebesst);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("cors,web", out string rch_error))
                return ShowError(rch_error);

            reset:
            if (string.IsNullOrEmpty(uri))
            {
                if (string.IsNullOrWhiteSpace(title))
                    return OnError();

                #region Поиск
                var cache = await InvokeCache<List<(string title, string year, string uri, string s, string img)>>($"animebesst:search:{title}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";
                    string search = rch.enable ? await rch.Post($"{init.corsHost()}/index.php?do=search", data) : await Http.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null)
                        return res.Fail("search");

                    var rows = search.Split("id=\"sidebar\"")[0].Split("class=\"shortstory-listab\"");

                    var catalog = new List<(string title, string year, string uri, string s, string img)>(rows.Length);

                    foreach (string row in rows.Skip(1))
                    {
                        if (row.Contains("Новости"))
                            continue;

                        var g = Regex.Match(row, "class=\"shortstory-listab-title\"><a href=\"(https?://[^\"]+\\.html)\">([^<]+)</a>").Groups;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                        {
                            string season = "0";
                            if (g[2].Value.Contains("сезон"))
                            {
                                season = Regex.Match(g[2].Value, "([0-9]+) сезон").Groups[1].Value;
                                if (string.IsNullOrEmpty(season))
                                    season = "1";
                            }

                            string img = Regex.Match(row, "<img class=\"img-fit lozad\" data-src=\"([^\"]+)\"").Groups[1].Value;
                            if (string.IsNullOrEmpty(img))
                                img = null;

                            catalog.Add((g[2].Value, Regex.Match(row, "\">([0-9]{4})</a>").Groups[1].Value, g[1].Value, season, img));
                        }
                    }

                    if (catalog.Count == 0 && !search.Contains(">Поиск по сайту<"))
                        return res.Fail("catalog");

                    return catalog;
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                if (cache.Value != null && cache.Value.Count == 0)
                    return OnError();

                if (!similar && cache.Value != null && cache.Value.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/animebesst?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(cache.Value[0].uri)}&s={cache.Value[0].s}"));

                return OnResult(cache, () =>
                {
                    var stpl = new SimilarTpl(cache.Value.Count);

                    foreach (var res in cache.Value)
                    {
                        string _u = $"{host}/lite/animebesst?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}";
                        stpl.Append(res.title, res.year, string.Empty, _u, PosterApi.Size(res.img));
                    }

                    return rjson ? stpl.ToJson() : stpl.ToHtml();

                }, gbcache: !rch.enable);
                #endregion
            }
            else 
            {
                #region Серии
                var cache = await InvokeCache<List<(string episode, string name, string uri)>>($"animebesst:playlist:{uri}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string news = rch.enable ? await rch.Get(uri) : await Http.Get(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (news == null)
                        return res.Fail("news");

                    string videoList = Regex.Match(news, "var videoList ?=([^\n\r]+)").Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(videoList))
                        return res.Fail("videoList");

                    var links = new List<(string episode, string name, string uri)>(5);
                    var match = Regex.Match(videoList, "\"id\":\"([0-9]+)( [^\"]+)?\",\"link\":\"(https?:)?\\\\/\\\\/([^\"]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[4].Value))
                            links.Add((match.Groups[1].Value, match.Groups[2].Value.Trim(), match.Groups[4].Value.Replace("\\", "")));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return res.Fail("links");

                    return links;
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                return OnResult(cache, () =>
                {
                    var etpl = new EpisodeTpl(cache.Value.Count);
                    string sArhc = s.ToString();

                    foreach (var l in cache.Value)
                    {
                        string name = string.IsNullOrEmpty(l.name) ? $"{l.episode} серия" : $"{l.episode} {l.name}";
                        string voice_name = !string.IsNullOrEmpty(l.name) ? Regex.Replace(l.name, "(^\\(|\\)$)", "") : "";

                        string link = accsArgs($"{host}/lite/animebesst/video.m3u8?uri={HttpUtility.UrlEncode(l.uri)}&title={HttpUtility.UrlEncode(title)}");

                        etpl.Append(name, $"{title} / {name}", sArhc, l.episode, link, "call", streamlink: $"{link}&play=true", voice_name: Regex.Unescape(voice_name));
                    }

                    return rjson ? etpl.ToJson() : etpl.ToHtml();

                }, gbcache: !rch.enable);
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animebesst/video.m3u8")]
        async public ValueTask<ActionResult> Video(string uri, string title, bool play)
        {
            var init = await loadKit(AppInit.conf.Animebesst);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("cors,web", out string rch_error))
                return ShowError(rch_error);

            if (rch.IsNotConnected() && init.rhub_fallback && play)
                rch.Disabled();

            var cache = await InvokeCache<string>($"animebesst:video:{uri}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string iframe;
                if (rch.enable)
                {
                    iframe = await rch.Get(init.cors($"https://{uri}"), headers: httpHeaders(init));
                }
                else
                {
                    iframe = await Http.Get(init.cors($"https://{uri}"), referer: init.host, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init), httpversion: 2);
                }

                if (iframe == null)
                    return res.Fail("iframe");

                string hls = Regex.Match(iframe, "file:\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(hls))
                    return res.Fail("hls");

                return hls;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg, gbcache: !rch.enable);

            string link = HostStreamProxy(init, cache.Value, proxy: proxyManager.Get());

            if (play)
                return RedirectToPlay(link);

            return ContentTo(VideoTpl.ToJson("play", link, title, vast: init.vast));
        }
        #endregion
    }
}
