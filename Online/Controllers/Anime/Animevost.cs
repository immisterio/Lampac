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
        ProxyManager proxyManager = new ProxyManager("animevost", AppInit.conf.Animevost);

        [HttpGet]
        [Route("lite/animevost")]
        async public Task<ActionResult> Index(string rchtype, string title, int year, string uri, int s, bool rjson = false)
        {
            var init = AppInit.conf.Animevost;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var rch = new RchClient(HttpContext, host, init.rhub);

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                string memkey = $"animevost:search:{title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri, string s)> catalog))
                {
                    if (rch.IsNotSupport(rchtype, "web", out string rch_error))
                        return ShowError(rch_error);

                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string data = $"do=search&subaction=search&search_start=0&full_search=1&result_from=1&story={HttpUtility.UrlEncode(title)}&all_word_seach=1&titleonly=3&searchuser=&replyless=0&replylimit=0&searchdate=0&beforeafter=after&sortby=date&resorder=desc&showposts=0&catlist%5B%5D=0";
                    string search = init.rhub ? await rch.Post($"{init.corsHost()}/index.php?do=search", data) : await HttpClient.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null)
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    catalog = new List<(string title, string year, string uri, string s)>();

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
                        return OnError();

                    if (!init.rhub)
                        proxyManager.Success();

                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 0)
                    return OnError();

                if (catalog.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/animevost?rjson={rjson}&rchtype={rchtype}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog[0].uri)}&s={catalog[0].s}"));

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animevost?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}");

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animevost:playlist:{uri}";
                if (!hybridCache.TryGetValue(memKey, out List<(string episode, string id)> links))
                {
                    if (rch.IsNotSupport(rchtype, "web", out string rch_error))
                        return ShowError(rch_error);

                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string news = init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (news == null)
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    string data = Regex.Match(news, "var data = ([^\n\r]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(data))
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    links = new List<(string episode, string id)>();
                    var match = Regex.Match(data, "\"([^\"]+)\":\"([0-9]+)\",");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                            links.Add((match.Groups[1].Value, match.Groups[2].Value));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return OnError();

                    if (!init.rhub)
                        proxyManager.Success();

                    hybridCache.Set(memKey, links, cacheTime(30, init: init));
                }

                var etpl = new EpisodeTpl();

                foreach (var l in links)
                {
                    string link = $"{host}/lite/animevost/video?id={l.id}&title={HttpUtility.UrlEncode(title)}";
                    string streamlink = init.rhub ? null : accsArgs($"{link}&play=true");

                    etpl.Append(l.episode, title, s.ToString(), Regex.Match(l.episode, "^([0-9]+)").Groups[1].Value, link, "call", streamlink: streamlink);
                }

                return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animevost/video")]
        async public Task<ActionResult> Video(int id, string title, bool play)
        {
            var init = AppInit.conf.Animevost;
            if (!init.enable)
                return OnError();

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            string memKey = $"animevost:video:{id}";
            if (!hybridCache.TryGetValue(memKey, out string mp4))
            {
                var rch = new RchClient(HttpContext, host, init.rhub);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string uri = $"{init.corsHost()}/frame5.php?play={id}&old=1";
                string iframe = init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                mp4 = Regex.Match(iframe ?? "", "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">720p").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(mp4))
                    mp4 = Regex.Match(iframe ?? "" , "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">480p").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(mp4))
                    return OnError(proxyManager, refresh_proxy: !init.rhub);

                if (!init.rhub)
                    proxyManager.Success();

                hybridCache.Set(memKey, mp4, cacheTime(20, init: init));
            }

            string link = HostStreamProxy(init, mp4, proxy: proxyManager.Get(), plugin: "animevost");

            if (play)
                return Redirect(link);

            return Content("{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + title + "\"}", "application/json; charset=utf-8");
        }
        #endregion
    }
}
