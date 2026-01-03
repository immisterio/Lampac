using Microsoft.AspNetCore.Mvc;

namespace Online.Controllers
{
    public class Animevost : BaseOnlineController
    {
        public Animevost() : base(AppInit.conf.Animevost) { }

        [HttpGet]
        [Route("lite/animevost")]
        async public Task<ActionResult> Index(string title, int year, string uri, int s, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            rhubFallback:
            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                var cache = await InvokeCacheResult<List<(string title, string year, string uri, string s, string img)>>($"animevost:search:{title}:{similar}", 40, async e =>
                {
                    string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";

                    string search = await httpHydra.Post($"{init.corsHost()}/index.php?do=search", data);

                    if (search == null)
                        return e.Fail("search", refresh_proxy: true);

                    var rx = new RxEnumerate("class=\"shortstory\"", search, 1);
                    int rxCount = rx.Count();

                    var smlr = new List<(string title, string year, string uri, string s, string img)>(rxCount);
                    var catalog = new List<(string title, string year, string uri, string s, string img)>(rxCount);

                    foreach (string row in rx.Rows())
                    {
                        var g = Regex.Match(row, "<a href=\"(https?://[^\"]+\\.html)\">([^<]+)</a>", RegexOptions.Compiled).Groups;
                        string animeyear = Regex.Match(row, "<strong>Год выхода: ?</strong>([0-9]{4})</p>", RegexOptions.Compiled).Groups[1].Value;
                        string img = Regex.Match(row, " src=\"(/uploads/[^\"]+)\"", RegexOptions.Compiled).Groups[1].Value;
                        if (!string.IsNullOrEmpty(img))
                            img = init.host + img;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                        {
                            string season = Regex.Match(g[2].Value, "([0-9 ]+) ?nd ", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                            if (string.IsNullOrEmpty(season))
                            {
                                season = Regex.Match(g[2].Value, "Season ([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.Trim();
                                if (string.IsNullOrEmpty(season))
                                    season = "1";
                            }

                            smlr.Add((g[2].Value, animeyear, g[1].Value, season, string.IsNullOrEmpty(img) ? null : img));

                            if (animeyear == year.ToString() && StringConvert.SearchName(g[2].Value).Contains(StringConvert.SearchName(title)))
                                catalog.Add((g[2].Value, animeyear, g[1].Value, season, null));
                        }
                    }

                    if (catalog.Count == 0 && smlr.Count == 0)
                        return e.Fail("catalog");

                    if (!similar && catalog.Count > 0)
                        return e.Success(catalog);

                    return e.Success(smlr);
                });

                if (IsRhubFallback(cache))
                    goto rhubFallback;

                if (!similar && cache.Value != null && cache.Value.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/animevost?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(cache.Value[0].uri)}&s={cache.Value[0].s}"));

                return await ContentTpl(cache, () =>
                {
                    if (cache.Value.Count == 0)
                        return default;

                    var stpl = new SimilarTpl(cache.Value.Count);

                    foreach (var res in cache.Value)
                    {
                        string uri = $"{host}/lite/animevost?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}";
                        stpl.Append(res.title, res.year, string.Empty, uri, PosterApi.Size(res.img));
                    }

                    return stpl;
                });
                #endregion
            }
            else 
            {
                #region Серии
                var cache = await InvokeCacheResult<List<(string episode, string id)>>($"animevost:playlist:{uri}", 30, async e =>
                {
                    string news = await httpHydra.Get(uri);

                    if (news == null)
                        return e.Fail("news", refresh_proxy: true);

                    string data = Regex.Match(news, "var data = ([^\n\r]+)", RegexOptions.Compiled).Groups[1].Value;
                    if (string.IsNullOrEmpty(data))
                        return e.Fail("data", refresh_proxy: true);

                    var match = Regex.Match(data, "\"([^\"]+)\":\"([0-9]+)\",", RegexOptions.Compiled);
                    var links = new List<(string episode, string id)>(match.Length);

                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                            links.Add((match.Groups[1].Value, match.Groups[2].Value));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return e.Fail("links");

                    return e.Success(links);
                });

                if (IsRhubFallback(cache))
                    goto rhubFallback;

                return await ContentTpl(cache, () =>
                {
                    var etpl = new EpisodeTpl(cache.Value.Count);

                    foreach (var l in cache.Value)
                    {
                        string link = $"{host}/lite/animevost/video?id={l.id}&title={HttpUtility.UrlEncode(title)}";

                        etpl.Append(l.episode, title, s.ToString(), Regex.Match(l.episode, "^([0-9]+)", RegexOptions.Compiled).Groups[1].Value, link, "call", streamlink: accsArgs($"{link}&play=true"));
                    }

                    return etpl;
                });
                #endregion
            }
        }

        #region Video
        [HttpGet]
        [Route("lite/animevost/video")]
        async public ValueTask<ActionResult> Video(int id, string title, bool play)
        {
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            if (rch != null)
            {
                if (rch.IsNotConnected())
                {
                    if (init.rhub_fallback && play)
                        rch.Disabled();
                    else
                        return ContentTo(rch.connectionMsg);
                }

                if (!play && rch.IsRequiredConnected())
                    return ContentTo(rch.connectionMsg);

                if (rch.IsNotSupport(out string rch_error))
                    return ShowError(rch_error);
            }

            rhubFallback:
            var cache = await InvokeCacheResult<List<(string l, string q)>>($"animevost:video:{id}", 20, async e =>
            {
                string uri = $"{init.corsHost()}/frame5.php?play={id}&old=1";

                string iframe = await httpHydra.Get(uri);

                var links = new List<(string l, string q)>(2);

                string mp4 = Regex.Match(iframe ?? "", "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">720p", RegexOptions.Compiled).Groups[1].Value;
                if (!string.IsNullOrEmpty(mp4))
                    links.Add((mp4, "720p"));

                mp4 = Regex.Match(iframe ?? "", "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">480p", RegexOptions.Compiled).Groups[1].Value;
                if (!string.IsNullOrEmpty(mp4))
                    links.Add((mp4, "480p"));

                if (links.Count == 0)
                    return e.Fail("mp4", refresh_proxy: true);

                return e.Success(links);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (cache.IsSuccess && play)
                return Redirect(HostStreamProxy(cache.Value[0].l));

            return OnResult(cache, () =>
            {
                string link = HostStreamProxy(cache.Value[0].l);
                return VideoTpl.ToJson("play", link, title, vast: init.vast);
            });
        }
        #endregion
    }
}
