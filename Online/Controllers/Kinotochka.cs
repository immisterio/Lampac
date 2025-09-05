using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Kinotochka;

namespace Online.Controllers
{
    public class Kinotochka : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, int serial, string newsuri, int s = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Kinotochka);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            // enable 720p
            string cookie = init.cookie;

            if (serial == 1)
            {
                if (s == -1)
                {
                    #region Сезоны
                    reset:
                    var cache = await InvokeCache<List<(string name, string uri, string season)>>($"kinotochka:seasons:{title}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                    {
                        if (rch.IsNotConnected())
                            return res.Fail(rch.connectionMsg);

                        List<(string, string, string)> links = null;

                        if (kinopoisk_id > 0) // https://kinovibe.co/embed.html
                        {
                            string uri = $"{init.corsHost()}/api/find-by-kinopoisk.php?kinopoisk={kinopoisk_id}";
                            var root = rch.enable ? await rch.Get<JArray>(uri, httpHeaders(init)) : await Http.Get<JArray>(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                            if (root == null || root.Count == 0)
                                return res.Fail("find-by-kinopoisk");

                            links = new List<(string, string, string)>(root.Count);
                            foreach (var item in root)
                            {
                                string url = item.Value<string>("url");
                                string sname = Regex.Match(url, "-([0-9]+)-sezon").Groups[1].Value;
                                if (!string.IsNullOrEmpty(sname))
                                    links.Add(($"{sname} сезон", $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={sname}&newsuri={HttpUtility.UrlEncode(url)}", sname));
                            }

                            if (links.Count == 0)
                                return res.Fail("links");
                        }
                        else
                        {
                            string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";
                            string search = rch.enable ? await rch.Post($"{init.corsHost()}/index.php?do=search", data, httpHeaders(init)) : await Http.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                            if (search == null)
                            {
                                if (!rch.enable)
                                    proxyManager.Refresh();

                                return res.Fail("search");
                            }

                            var rows = search.Split("sres-wrap clearfix");
                            links = new List<(string, string, string)>(rows.Length);

                            string stitle = StringConvert.SearchName(title);

                            foreach (string row in rows.Skip(1).Reverse())
                            {
                                var gname = Regex.Match(row, "<h2>([^<]+) (([0-9]+) Сезон) \\([0-9]{4}\\)</h2>", RegexOptions.IgnoreCase).Groups;

                                if (StringConvert.SearchName(gname[1].Value) == stitle)
                                {
                                    string uri = Regex.Match(row, "href=\"(https?://[^\"]+\\.html)\"").Groups[1].Value;
                                    if (string.IsNullOrWhiteSpace(uri))
                                        continue;

                                    links.Add((gname[2].Value.ToLower(), $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={gname[3].Value}&newsuri={HttpUtility.UrlEncode(uri)}", gname[3].Value));
                                }
                            }

                            if (links.Count == 0 && !search.Contains(">Поиск по сайту<"))
                                return res.Fail("links");
                        }

                        return links;
                    });

                    if (IsRhubFallback(cache, init))
                        goto reset;

                    return OnResult(cache, () =>
                    {
                        var tpl = new SeasonTpl(cache.Value.Count);

                        foreach (var l in cache.Value)
                            tpl.Append(l.name, l.uri, l.season);

                        return rjson ? tpl.ToJson() : tpl.ToHtml();

                    }, gbcache: !rch.enable);
                    #endregion
                }
                else
                {
                    #region Серии
                    reset: 
                    var cache = await InvokeCache<List<(string name, string uri)>>($"kinotochka:playlist:{newsuri}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                    {
                        if (rch.IsNotConnected())
                            return res.Fail(rch.connectionMsg);

                        string news = rch.enable ? await rch.Get(newsuri, httpHeaders(init)) : await Http.Get(newsuri, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                        if (news == null)
                        {
                            if (!rch.enable)
                                proxyManager.Refresh();

                            return res.Fail("news");
                        }

                        string filetxt = Regex.Match(news, "file:\"(https?://[^\"]+\\.txt)\"").Groups[1].Value;
                        if (string.IsNullOrEmpty(filetxt))
                            return res.Fail("filetxt");

                        var root = rch.enable ? await rch.Get<JObject>(filetxt, httpHeaders(init)) : await Http.Get<JObject>(filetxt, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                        if (root == null)
                        {
                            if (!rch.enable)
                                proxyManager.Refresh();

                            return res.Fail("root");
                        }

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                            return res.Fail("playlist");

                        var links = new List<(string name, string uri)>(playlist.Count);

                        foreach (var pl in playlist)
                        {
                            string name = pl.Value<string>("comment");
                            string file = pl.Value<string>("file");
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(file))
                            {
                                if (file.Contains("].mp4"))
                                    file = Regex.Replace(file, "\\[[^\\]]+,([0-9]+)\\]\\.mp4$", "$1.mp4");

                                links.Add((name.Split("<")[0].Trim(), file));
                            }
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

                        foreach (var l in cache.Value)
                            etpl.Append(l.name, title, s.ToString(), Regex.Match(l.name, "^([0-9]+)").Groups[1].Value, HostStreamProxy(init, l.uri, proxy: proxy), vast: init.vast);

                        return rjson ? etpl.ToJson() : etpl.ToHtml();

                    }, gbcache: !rch.enable);
                    #endregion
                }
            }
            else
            {
                #region Фильм
                if (kinopoisk_id == 0)
                    return OnError();

                reset:
                var cache = await InvokeCache<EmbedModel>($"kinotochka:view:{kinopoisk_id}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string uri = $"{init.corsHost()}/embed/kinopoisk/{kinopoisk_id}";
                    string embed = rch.enable ? await rch.Get(uri, httpHeaders(init)) : await Http.Get(uri, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                    if (embed == null)
                    {
                        if (!rch.enable)
                            proxyManager.Refresh();

                        return res.Fail("embed");
                    }

                    string file = Regex.Match(embed, "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(file))
                        return res.Fail("file");

                    foreach (string f in file.Split(",").Reverse())
                    {
                        if (string.IsNullOrWhiteSpace(f))
                            continue;

                        file = f;
                        break;
                    }

                    return new EmbedModel() { content = file };
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                return OnResult(cache, () => 
                {
                    var mtpl = new MovieTpl(title, original_title, 1);
                    mtpl.Append("По умолчанию", HostStreamProxy(init, cache.Value.content, proxy: proxy), vast: init.vast);

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, gbcache: !rch.enable);
                #endregion
            }
        }
    }
}
