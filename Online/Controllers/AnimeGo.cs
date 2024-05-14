using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class AnimeGo : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("animego", AppInit.conf.AnimeGo);

        [HttpGet]
        [Route("lite/animego")]
        async public Task<ActionResult> Index(string title, int year, int pid, int s, string t, string account_email)
        {
            var init = AppInit.conf.AnimeGo;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            if (pid == 0)
            {
                #region Поиск
                string memkey = $"animego:search:{title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string pid, string s)> catalog))
                {
                    string search = await HttpClient.Get($"{init.corsHost()}/search/anime?q={HttpUtility.UrlEncode(title)}", timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init), httpversion: 2);
                    if (search == null)
                        return OnError(proxyManager);

                    catalog = new List<(string title, string year, string pid, string s)>();

                    foreach (string row in search.Split("class=\"p-poster__stack\"").Skip(1))
                    {
                        string player_id = Regex.Match(row, "data-ajax-url=\"/[^\"]+-([0-9]+)\"").Groups[1].Value;
                        string name = Regex.Match(row, "card-title text-truncate\"><a [^>]+>([^<]+)<").Groups[1].Value;
                        string animeyear = Regex.Match(row, "class=\"anime-year\"><a [^>]+>([0-9]{4})<").Groups[1].Value;

                        if (!string.IsNullOrWhiteSpace(player_id) && !string.IsNullOrWhiteSpace(name) && name.ToLower().Contains(title.ToLower()))
                        {
                            string season = "0";
                            if (animeyear == year.ToString() && name.ToLower() == title.ToLower())
                                season = "1";

                            catalog.Add((name, Regex.Match(row, ">([0-9]{4})</a>").Groups[1].Value, player_id, season));
                        }
                    }

                    if (catalog.Count == 0)
                        return OnError();

                    proxyManager.Success();
                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={catalog[0].pid}&s={catalog[0].s}&account_email={HttpUtility.UrlEncode(account_email)}");

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={res.pid}&s={res.s}");

                return Content(stpl.ToHtml(), "text/html; charset=utf-8");
                #endregion
            }
            else 
            {
                #region Серии
                bool firstjson = true;
                string html = "<div class=\"videos__line\">";

                string memKey = $"animego:playlist:{pid}";
                if (!hybridCache.TryGetValue(memKey, out (string translation, List<(string episode, string uri)> links, List<(string name, string id)> translations) cache))
                {
                    #region content
                    var player = await HttpClient.Get<JObject>($"{init.corsHost()}/anime/{pid}/player?_allow=true", timeoutSeconds: 10, proxy: proxyManager.Get(), httpversion: 2, headers: httpHeaders(init, HeadersModel.Init(
                        ("cache-control", "no-cache"),
                        ("dnt", "1"),
                        ("pragma", "no-cache"),
                        ("referer", $"{init.host}/"),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    )));

                    string content = player?.Value<string>("content");
                    if (string.IsNullOrWhiteSpace(content))
                        return OnError(proxyManager);
                    #endregion

                    var g = Regex.Match(content, "data-player=\"(https?:)?//(aniboom\\.[^/]+)/embed/([^\"\\?&]+)\\?episode=1\\&amp;translation=([0-9]+)\"").Groups;
                    if (string.IsNullOrWhiteSpace(g[2].Value) || string.IsNullOrWhiteSpace(g[3].Value) || string.IsNullOrWhiteSpace(g[4].Value))
                        return OnError();

                    #region links
                    cache.links = new List<(string episode, string uri)>();
                    var match = Regex.Match(content, "data-episode=\"([0-9]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value))
                            cache.links.Add((match.Groups[1].Value, $"video.m3u8?host={g[2].Value}&token={g[3].Value}&e={match.Groups[1].Value}"));

                        match = match.NextMatch();
                    }

                    if (cache.links.Count == 0)
                        return OnError();
                    #endregion

                    #region translation / translations
                    cache.translation = g[4].Value;
                    cache.translations = new List<(string name, string id)>();

                    match = Regex.Match(content, "data-player=\"(https?:)?//aniboom\\.[^/]+/embed/[^\"\\?&]+\\?episode=[0-9]+\\&amp;translation=([0-9]+)\"[\n\r\t ]+data-provider=\"[0-9]+\"[\n\r\t ]+data-provide-dubbing=\"([0-9]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[2].Value) && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                        {
                            string name = Regex.Match(content, $"data-dubbing=\"{match.Groups[3].Value}\"><span [^>]+>[\n\r\t ]+([^\n\r<]+)").Groups[1].Value.Trim();
                            if (!string.IsNullOrWhiteSpace(name))
                                cache.translations.Add((name, match.Groups[2].Value));
                        }

                        match = match.NextMatch();
                    }
                    #endregion

                    proxyManager.Success();
                    hybridCache.Set(memKey, cache, cacheTime(30, init: init));
                }

                #region Перевод
                if (string.IsNullOrWhiteSpace(t))
                    t = cache.translation;

                foreach (var translation in cache.translations)
                {
                    string link = $"{host}/lite/animego?pid={pid}&title={HttpUtility.UrlEncode(title)}&s={s}&t={translation.id}";
                    string active = t == translation.id ? "active" : "";

                    html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation.name + "</div>";
                }

                html += "</div><div class=\"videos__line\">";
                #endregion

                foreach (var l in cache.links)
                {
                    string hls = $"{host}/lite/animego/{l.uri}&t={t ?? cache.translation}&account_email={HttpUtility.UrlEncode(account_email)}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + l.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + $"{title} ({l.episode} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{l.episode} серия" + "</div></div>";
                    firstjson = true;
                }

                return Content(html + "</div>", "text/html; charset=utf-8");
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animego/video.m3u8")]
        async public Task<ActionResult> Video(string host, string token, string t, int e)
        {
            var init = AppInit.conf.AnimeGo;

            if (!init.enable)
                return OnError();

            string memKey = $"animego:video:{token}:{t}:{e}";
            if (!hybridCache.TryGetValue(memKey, out string hls))
            {
                string embed = await HttpClient.Get($"https://{host}/embed/{token}?episode={e}&translation={t}", timeoutSeconds: 10, proxy: proxyManager.Get(), httpversion: 2, headers: httpHeaders(init, HeadersModel.Init(
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("referer", $"{init.host}/"),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-requested-with", "XMLHttpRequest")
                )));

                if (string.IsNullOrWhiteSpace(embed))
                    return OnError(proxyManager);

                embed = embed.Replace("&quot;", "\"").Replace("\\", "");

                hls = Regex.Match(embed, "\"hls\":\"\\{\"src\":\"(https?:)?(//[^\"]+\\.m3u8)\"").Groups[2].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return OnError(proxyManager);

                hls = "https:" + hls;

                proxyManager.Success();
                hybridCache.Set(memKey, hls, cacheTime(30, init: init));
            }

            return Redirect(HostStreamProxy(init, hls, proxy: proxyManager.Get(), plugin: "animego", headers: HeadersModel.Init(
                ("origin", "https://aniboom.one"),
                ("referer", "https://aniboom.one/")
            )));
        }
        #endregion
    }
}
