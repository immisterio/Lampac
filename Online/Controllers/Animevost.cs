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
        async public Task<ActionResult> Index(string title, int year, string uri, int s, string account_email)
        {
            var init = AppInit.conf.Animevost;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                string memkey = $"animevost:search:{title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri, string s)> catalog))
                {
                    string search = await HttpClient.Post($"{init.corsHost()}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=1&result_from=1&story={HttpUtility.UrlEncode(title)}&all_word_seach=1&titleonly=3&searchuser=&replyless=0&replylimit=0&searchdate=0&beforeafter=after&sortby=date&resorder=desc&showposts=0&catlist%5B%5D=0", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null)
                        return OnError(proxyManager);

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

                    proxyManager.Success();
                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 0)
                    return OnError();

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animevost?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog[0].uri)}&s={catalog[0].s}&account_email={HttpUtility.UrlEncode(account_email)}");

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animevost?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}");

                return Content(stpl.ToHtml(), "text/html; charset=utf-8");
                #endregion
            }
            else 
            {
                #region Серии
                bool firstjson = true;
                string html = "<div class=\"videos__line\">";

                string memKey = $"animevost:playlist:{uri}";
                if (!hybridCache.TryGetValue(memKey, out List<(string episode, string id)> links))
                {
                    string news = await HttpClient.Get(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (news == null)
                        return OnError(proxyManager);

                    string data = Regex.Match(news, "var data = ([^\n\r]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(data))
                        return OnError(proxyManager);

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

                    proxyManager.Success();
                    hybridCache.Set(memKey, links, cacheTime(30, init: init));
                }

                foreach (var l in links)
                {
                    string link = $"{host}/lite/animevost/video?id={l.id}&account_email={HttpUtility.UrlEncode(account_email)}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(l.episode, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.episode})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + l.episode + "</div></div>";
                    firstjson = true;
                }

                return Content(html + "</div>", "text/html; charset=utf-8");
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animevost/video")]
        async public Task<ActionResult> Video(int id)
        {
            var init = AppInit.conf.Animevost;

            if (!init.enable)
                return OnError();

            string memKey = $"animevost:video:{id}";
            if (!hybridCache.TryGetValue(memKey, out string mp4))
            {
                string iframe = await HttpClient.Get($"{init.corsHost()}/frame5.php?play={id}&old=1", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));

                mp4 = Regex.Match(iframe ?? "", "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">720p").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(mp4))
                    mp4 = Regex.Match(iframe ?? "" , "download=\"invoice\"[^>]+href=\"(https?://[^\"]+)\">480p").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(mp4))
                    return OnError(proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, mp4, cacheTime(20, init: init));
            }

            return Redirect(HostStreamProxy(init, mp4, proxy: proxyManager.Get(), plugin: "animevost"));
        }
        #endregion
    }
}
