using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using System.Linq;
using System.Text.RegularExpressions;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class Animevost : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.Animevost);

        [HttpGet]
        [Route("lite/animevost")]
        async public Task<ActionResult> Index(string title, int year, string uri, int s, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Animevost);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);

            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                var cache = await InvokeCache<List<(string title, string year, string uri, string s)>>($"animevost:search:{title}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string data = $"do=search&subaction=search&search_start=0&full_search=1&result_from=1&story={HttpUtility.UrlEncode(title)}&all_word_seach=1&titleonly=3&searchuser=&replyless=0&replylimit=0&searchdate=0&beforeafter=after&sortby=date&resorder=desc&showposts=0&catlist%5B%5D=0";
                    string search = rch.enable ? await rch.Post($"{init.corsHost()}/index.php?do=search", data) : await HttpClient.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null)
                    {
                        if (!rch.enable)
                            proxyManager?.Refresh();

                        return res.Fail("search");
                    }

                    var catalog = new List<(string title, string year, string uri, string s)>();

                    foreach (string row in search.Split("class=\"shortstory\"").Skip(1))
                    {
                        var g = Regex.Match(row, "<a href=\"(https?://[^\"]+\\.html)\">([^<]+)</a>").Groups;
                        string animeyear = Regex.Match(row, "<strong>Год выхода: ?</strong>([0-9]{4})</p>").Groups[1].Value;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                        {
                            string season = "0";
                            if (animeyear == year.ToString() && g[2].Value.ToLower().StartsWith(title.ToLower()))
                                season = "1";

                            catalog.Add((g[2].Value, animeyear, g[1].Value, season));
                        }
                    }

                    if (catalog.Count == 0 && !search.Contains("Поиск по сайту"))
                        return res.Fail("catalog");

                    return catalog;
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                if (cache.Value != null && cache.Value.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/animevost?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(cache.Value[0].uri)}&s={cache.Value[0].s}"));

                return OnResult(cache, () =>
                {
                    if (cache.Value.Count == 0)
                        return string.Empty;

                    var stpl = new SimilarTpl(cache.Value.Count);

                    foreach (var res in cache.Value)
                        stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animevost?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}");

                    return rjson ? stpl.ToJson() : stpl.ToHtml();

                }, gbcache: !rch.enable);
                #endregion
            }
            else 
            {
                #region Серии
                var cache = await InvokeCache<List<(string episode, string id)>>($"animevost:playlist:{uri}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string news = rch.enable ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (news == null)
                    {
                        if (!rch.enable)
                            proxyManager?.Refresh();

                        return res.Fail("news");
                    }

                    string data = Regex.Match(news, "var data = ([^\n\r]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(data))
                    {
                        if (!rch.enable)
                            proxyManager?.Refresh();

                        return res.Fail("data");
                    }

                    var links = new List<(string episode, string id)>();
                    var match = Regex.Match(data, "\"([^\"]+)\":\"([0-9]+)\",");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                            links.Add((match.Groups[1].Value, match.Groups[2].Value));

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
                    var etpl = new EpisodeTpl();

                    foreach (var l in cache.Value)
                    {
                        string link = $"{host}/lite/animevost/video?id={l.id}&title={HttpUtility.UrlEncode(title)}";

                        etpl.Append(l.episode, title, s.ToString(), Regex.Match(l.episode, "^([0-9]+)").Groups[1].Value, link, "call", streamlink: accsArgs($"{link}&play=true"));
                    }

                    return rjson ? etpl.ToJson() : etpl.ToHtml();

                }, gbcache: !rch.enable);
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animevost/video")]
        async public Task<ActionResult> Video(int id, string title, bool play)
        {
            var init = await loadKit(AppInit.conf.Animevost);
            if (await IsBadInitialization(init))
                return badInitMsg;

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);

            var cache = await InvokeCache<List<(string l, string q)>>($"animevost:video:{id}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/frame5.php?play={id}&old=1";
                string iframe = rch.enable ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                var links = new List<(string l, string q)>(2);

                string mp4 = Regex.Match(iframe ?? "", "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">720p").Groups[1].Value;
                if (!string.IsNullOrEmpty(mp4))
                    links.Add((mp4, "720p"));

                mp4 = Regex.Match(iframe ?? "", "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">480p").Groups[1].Value;
                if (!string.IsNullOrEmpty(mp4))
                    links.Add((mp4, "480p"));

                if (links.Count == 0)
                {
                    if (!rch.enable)
                        proxyManager?.Refresh();

                    return res.Fail("mp4");
                }

                if (!rch.enable)
                    proxyManager.Success();

                return links;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            if (cache.IsSuccess && play)
                return Redirect(HostStreamProxy(init, cache.Value[0].l, proxy: proxyManager.Get()));

            return OnResult(cache, () =>
            {
                string link = HostStreamProxy(init, cache.Value[0].l, proxy: proxyManager.Get());
                return VideoTpl.ToJson("play", link, title, vast: init.vast);

            }, gbcache: !rch.enable);
        }
        #endregion
    }
}
