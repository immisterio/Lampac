using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using Lampac.Engine.CORE;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Model.Online.Kinotochka;

namespace Lampac.Controllers.LITE
{
    public class Kinotochka : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, int serial, string newsuri, int s = -1, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Kinotochka);
            if (IsBadInitialization(init, out ActionResult action, rch: true))
                return action;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            // enable 720p
            string cookie = init.cookie;

            if (serial == 1)
            {
                // https://kinovibe.co/embed.html

                if (s == -1)
                {
                    #region Сезоны
                    var cache = await InvokeCache<List<(string name, string uri, string season)>>($"kinotochka:seasons:{title}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                    {
                        if (rch.IsNotConnected())
                            return res.Fail(rch.connectionMsg);

                        string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";
                        string search = rch.enable ? await rch.Post($"{init.corsHost()}/index.php?do=search", data, httpHeaders(init)) : await HttpClient.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                        if (search == null)
                        {
                            if (!rch.enable)
                                proxyManager?.Refresh();

                            return res.Fail("search");
                        }

                        var links = new List<(string, string, string)>();

                        foreach (string row in search.Split("sres-wrap clearfix").Skip(1).Reverse())
                        {
                            var gname = Regex.Match(row, "<h2>([^<]+) (([0-9]+) Сезон) \\([0-9]{4}\\)</h2>", RegexOptions.IgnoreCase).Groups;

                            if (gname[1].Value.ToLower() == title.ToLower())
                            {
                                string uri = Regex.Match(row, "href=\"(https?://[^\"]+\\.html)\"").Groups[1].Value;
                                if (string.IsNullOrWhiteSpace(uri))
                                    continue;

                                links.Add((gname[2].Value.ToLower(), $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={gname[3].Value}&newsuri={HttpUtility.UrlEncode(uri)}", gname[3].Value));
                            }
                        }

                        if (links.Count == 0 && !search.Contains(">Поиск по сайту<"))
                            return res.Fail("links");

                        return links;
                    });

                    if (IsRhubFallback(cache, init))
                        goto reset;

                    return OnResult(cache, () =>
                    {
                        var tpl = new SeasonTpl();

                        foreach (var l in cache.Value)
                            tpl.Append(l.name, l.uri, l.season);

                        return rjson ? tpl.ToJson() : tpl.ToHtml();

                    }, gbcache: !rch.enable);
                    #endregion
                }
                else
                {
                    #region Серии
                    var cache = await InvokeCache<List<(string name, string uri)>>($"kinotochka:playlist:{newsuri}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                    {
                        if (rch.IsNotConnected())
                            return res.Fail(rch.connectionMsg);

                        string news = rch.enable ? await rch.Get(newsuri, httpHeaders(init)) : await HttpClient.Get(newsuri, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                        if (news == null)
                        {
                            if (!rch.enable)
                                proxyManager?.Refresh();

                            return res.Fail("news");
                        }

                        string filetxt = Regex.Match(news, "file:\"(https?://[^\"]+\\.txt)\"").Groups[1].Value;
                        if (string.IsNullOrEmpty(filetxt))
                            return res.Fail("filetxt");

                        var root = rch.enable ? await rch.Get<JObject>(filetxt, httpHeaders(init)) : await HttpClient.Get<JObject>(filetxt, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                        if (root == null)
                        {
                            if (!rch.enable)
                                proxyManager?.Refresh();

                            return res.Fail("root");
                        }

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                            return res.Fail("playlist");

                        var links = new List<(string name, string uri)>();

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
                        var etpl = new EpisodeTpl();

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

                var cache = await InvokeCache<EmbedModel>($"kinotochka:view:{kinopoisk_id}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string uri = $"{init.corsHost()}/embed/kinopoisk/{kinopoisk_id}";
                    string embed = rch.enable ? await rch.Get(uri, httpHeaders(init)) : await HttpClient.Get(uri, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                    if (embed == null)
                    {
                        if (!rch.enable)
                            proxyManager?.Refresh();

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
                    var mtpl = new MovieTpl(title);
                    mtpl.Append("По умолчанию", HostStreamProxy(init, cache.Value.content, proxy: proxy), vast: init.vast);

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, gbcache: !rch.enable);
                #endregion
            }
        }
    }
}
