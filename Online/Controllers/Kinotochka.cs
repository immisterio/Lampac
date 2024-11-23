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

namespace Lampac.Controllers.LITE
{
    public class Kinotochka : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(string rchtype, long kinopoisk_id, string title, int serial, string newsuri, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Kinotochka;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxyManager = new ProxyManager("kinotochka", init);
            var proxy = proxyManager.Get();

            // enable 720p
            string cookie = init.cookie;

            if (serial == 1)
            {
                // https://kinovibe.co/embed.html

                if (s == -1)
                {
                    #region Сезоны
                    string memKey = $"kinotochka:seasons:{title}";
                    if (!hybridCache.TryGetValue(memKey, out List<(string name, string uri, string season)> links))
                    {
                        if (rch.IsNotSupport(rchtype, "web", out string rch_error))
                            return ShowError(rch_error);

                        if (rch.IsNotConnected())
                            return ContentTo(rch.connectionMsg);

                        string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";
                        string search = init.rhub ? await rch.Post($"{init.corsHost()}/index.php?do=search", data) : await HttpClient.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                        if (search == null)
                            return OnError(proxyManager, refresh_proxy: !init.rhub);

                        links = new List<(string, string, string)>();

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
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memKey, links, cacheTime(30, init: init));
                    }

                    if (links.Count == 0)
                        return OnError();

                    var tpl = new SeasonTpl();

                    foreach (var l in links)
                        tpl.Append(l.name, l.uri, l.season);

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Серии
                    string memKey = $"kinotochka:playlist:{newsuri}";
                    if (!hybridCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        if (rch.IsNotSupport(rchtype, "web", out string rch_error))
                            return ShowError(rch_error);

                        if (rch.IsNotConnected())
                            return ContentTo(rch.connectionMsg);

                        string news = init.rhub ? await rch.Get(newsuri) : await HttpClient.Get(newsuri, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                        if (news == null)
                            return OnError(proxyManager, refresh_proxy: !init.rhub);

                        string filetxt = Regex.Match(news, "file:\"(https?://[^\"]+\\.txt)\"").Groups[1].Value;
                        if (string.IsNullOrEmpty(filetxt))
                            return OnError();

                        var root = init.rhub ? await rch.Get<JObject>(filetxt) : await HttpClient.Get<JObject>(filetxt, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                        if (root == null)
                            return OnError(proxyManager, refresh_proxy: !init.rhub);

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                            return OnError();

                        links = new List<(string name, string uri)>();

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
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memKey, links, cacheTime(30, init: init));
                    }

                    var etpl = new EpisodeTpl();

                    foreach (var l in links)
                        etpl.Append(l.name, title, s.ToString(), Regex.Match(l.name, "^([0-9]+)").Groups[1].Value, HostStreamProxy(init, l.uri, proxy: proxy));

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                    #endregion
                }
            }
            else
            {
                #region Фильм
                if (kinopoisk_id == 0)
                    return OnError();

                string memKey = $"kinotochka:view:{kinopoisk_id}";
                if (!hybridCache.TryGetValue(memKey, out string file))
                {
                    if (rch.IsNotSupport(rchtype, "web", out string rch_error))
                        return ShowError(rch_error);

                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string uri = $"{init.corsHost()}/embed/kinopoisk/{kinopoisk_id}";
                    string embed = init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 8, proxy: proxy, cookie: cookie, headers: httpHeaders(init));
                    if (embed == null)
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    file = Regex.Match(embed, "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(file))
                        return OnError();

                    foreach (string f in file.Split(",").Reverse())
                    {
                        if (string.IsNullOrWhiteSpace(f))
                            continue;

                        file = f;
                        break;
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, file, cacheTime(30, init: init));
                }

                var mtpl = new MovieTpl(title);
                mtpl.Append("По умолчанию", HostStreamProxy(init, file, proxy: proxy));

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
        }
    }
}
