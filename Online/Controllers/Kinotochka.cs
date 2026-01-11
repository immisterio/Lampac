using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine.RxEnumerate;
using Shared.Models.Online.Kinotochka;

namespace Online.Controllers
{
    public class Kinotochka : BaseOnlineController
    {
        public Kinotochka() : base(AppInit.conf.Kinotochka) { }

        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int serial, string newsuri, int s = -1)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            // enable 720p
            string cookie = init.cookie;

            if (serial == 1)
            {
                if (s == -1)
                {
                    #region Сезоны
                    rhubFallback:

                    var cache = await InvokeCacheResult<List<(string name, string uri, string season)>>($"kinotochka:seasons:{title}", 30, async e =>
                    {
                        List<(string, string, string)> links = null;

                        if (kinopoisk_id > 0) // https://kinovibe.co/embed.html
                        {
                            var root = await httpHydra.Get<JArray>($"{init.corsHost()}/api/find-by-kinopoisk.php?kinopoisk={kinopoisk_id}");

                            if (root == null || root.Count == 0)
                                return e.Fail("find-by-kinopoisk", refresh_proxy: true);

                            links = new List<(string, string, string)>(root.Count);
                            foreach (var item in root)
                            {
                                string url = item.Value<string>("url");
                                string sname = Regex.Match(url, "-([0-9]+)-sezon").Groups[1].Value;
                                if (!string.IsNullOrEmpty(sname))
                                    links.Add(($"{sname} сезон", $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={sname}&newsuri={HttpUtility.UrlEncode(url)}", sname));
                            }

                            if (links.Count == 0)
                                return e.Fail("links");
                        }
                        else
                        {
                            bool reqOk = false;
                            string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";

                            await httpHydra.PostSpan($"{init.corsHost()}/index.php?do=search", data, search => 
                            {
                                reqOk = search.Contains(">Поиск по сайту<", StringComparison.OrdinalIgnoreCase);

                                var rx = Rx.Split("sres-wrap clearfix", search, 1);
                                links = new List<(string, string, string)>(rx.Count);

                                string stitle = StringConvert.SearchName(title);

                                foreach (var row in rx.Rows())
                                {
                                    var gname = row.Groups("<h2>([^<]+) (([0-9]+) Сезон) \\([0-9]{4}\\)</h2>", RegexOptions.IgnoreCase);

                                    if (StringConvert.SearchName(gname[1].Value) == stitle)
                                    {
                                        string uri = row.Match("href=\"(https?://[^\"]+\\.html)\"");
                                        if (string.IsNullOrWhiteSpace(uri))
                                            continue;

                                        links.Add((gname[2].Value.ToLower(), $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={gname[3].Value}&newsuri={HttpUtility.UrlEncode(uri)}", gname[3].Value));
                                    }
                                }
                            });

                            if (links == null || links.Count == 0)
                                return e.Fail("links", refresh_proxy: !reqOk);
                        }

                        links.Reverse();
                        return e.Success(links);
                    });

                    if (IsRhubFallback(cache))
                        goto rhubFallback;

                    return await ContentTpl(cache, () =>
                    {
                        var tpl = new SeasonTpl(cache.Value.Count);

                        foreach (var l in cache.Value)
                            tpl.Append(l.name, l.uri, l.season);

                        return tpl;
                    });
                    #endregion
                }
                else
                {
                    #region Серии
                    rhubFallback:

                    var cache = await InvokeCacheResult<List<(string name, string uri)>>($"kinotochka:playlist:{newsuri}", 30, async e =>
                    {
                        string filetxt = null;

                        await httpHydra.GetSpan(newsuri, addheaders: HeadersModel.Init("cookie", cookie), safety: !string.IsNullOrEmpty(cookie), spanAction: news => 
                        {
                            filetxt = Rx.Match(news, "file:\"(https?://[^\"]+\\.txt)\"");
                        });

                        if (string.IsNullOrEmpty(filetxt))
                            return e.Fail("filetxt", refresh_proxy: true);

                        var root = await httpHydra.Get<JObject>(filetxt, addheaders: HeadersModel.Init("cookie", cookie), safety: !string.IsNullOrEmpty(cookie));

                        if (root == null)
                            return e.Fail("root", refresh_proxy: true);

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                            return e.Fail("playlist");

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
                            return e.Fail("links");

                        return e.Success(links);
                    });

                    if (IsRhubFallback(cache, safety: !string.IsNullOrEmpty(cookie)))
                        goto rhubFallback;

                    return await ContentTpl(cache, () =>
                    {
                        var etpl = new EpisodeTpl(cache.Value.Count);

                        foreach (var l in cache.Value)
                            etpl.Append(l.name, title, s.ToString(), Regex.Match(l.name, "^([0-9]+)").Groups[1].Value, HostStreamProxy(l.uri), vast: init.vast);

                        return etpl;
                    });
                    #endregion
                }
            }
            else
            {
                #region Фильм
                if (kinopoisk_id == 0)
                    return OnError();

                rhubFallback:
                var cache = await InvokeCacheResult<EmbedModel>($"kinotochka:view:{kinopoisk_id}", 30, async e =>
                {
                    string file = null;

                    await httpHydra.GetSpan($"{init.corsHost()}/embed/kinopoisk/{kinopoisk_id}", addheaders: HeadersModel.Init("cookie", cookie), safety: !string.IsNullOrEmpty(cookie), spanAction: embed => 
                    {
                        file = Rx.Match(embed, "id:\"playerjshd\", file:\"(https?://[^\"]+)\"");
                        if (string.IsNullOrEmpty(file))
                            return;

                        foreach (string f in file.Split(",").Reverse())
                        {
                            if (string.IsNullOrWhiteSpace(f))
                                continue;

                            file = f;
                            break;
                        }
                    });

                    if (string.IsNullOrEmpty(file))
                        return e.Fail("file", refresh_proxy: true);

                    return e.Success(new EmbedModel() { content = file });
                });

                if (IsRhubFallback(cache, safety: !string.IsNullOrEmpty(cookie)))
                    goto rhubFallback;

                return await ContentTpl(cache, () => 
                {
                    var mtpl = new MovieTpl(title, original_title, 1);
                    mtpl.Append("По умолчанию", HostStreamProxy(cache.Value.content), vast: init.vast);

                    return mtpl;
                });
                #endregion
            }
        }
    }
}
