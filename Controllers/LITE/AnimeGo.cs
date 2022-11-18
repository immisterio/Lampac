using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Lampac.Controllers.LITE
{
    public class AnimeGo : BaseController
    {
        [HttpGet]
        [Route("lite/animego")]
        async public Task<ActionResult> Index(string title, int year, int pid, int s)
        {
            if (!AppInit.conf.AnimeGo.enable || string.IsNullOrWhiteSpace(title))
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (pid == 0)
            {
                #region Поиск
                string memkey = $"animego:search:{title}";
                if (!memoryCache.TryGetValue(memkey, out List<(string title, string pid, string s)> catalog))
                {
                    string search = await HttpClient.Get($"{AppInit.conf.AnimeGo.host}/search/anime?q={HttpUtility.UrlEncode(title)}", timeoutSeconds: 10, useproxy: AppInit.conf.AnimeGo.useproxy);
                    if (search == null)
                        return Content(string.Empty);

                    catalog = new List<(string title, string pid, string s)>();

                    foreach (string row in search.Split("class=\"p-poster__stack\"").Skip(1))
                    {
                        string player_id = Regex.Match(row, "data-ajax-url=\"/[^\"]+-([0-9]+)\"").Groups[1].Value;
                        string name = Regex.Match(row, "card-title text-truncate\"><a [^>]+>([^<]+)<").Groups[1].Value;
                        string animeyear = Regex.Match(row, "class=\"anime-year\"><a [^>]+>([0-9]{4})<").Groups[1].Value;

                        if (!string.IsNullOrWhiteSpace(player_id) && !string.IsNullOrWhiteSpace(name))
                        {
                            string season = "0";
                            if (animeyear == year.ToString() && name.ToLower() == title.ToLower())
                                season = "1";

                            catalog.Add((name, player_id, season));
                        }
                    }

                    if (catalog.Count == 0)
                        return Content(string.Empty);

                    memoryCache.Set(memkey, catalog, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
                }

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={catalog[0].pid}&s={catalog[0].s}");

                foreach (var res in catalog)
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/animego?title={HttpUtility.UrlEncode(title)}&pid={res.pid}&s={res.s}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + res.title + "</div></div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animego:playlist:{pid}";
                if (!memoryCache.TryGetValue(memKey, out List<(string episode, string uri)> links))
                {
                    var player = await HttpClient.Get<JObject>($"{AppInit.conf.AnimeGo.host}/anime/{pid}/player?_allow=true", timeoutSeconds: 10, useproxy: AppInit.conf.AnimeGo.useproxy, addHeaders: new List<(string name, string val)>() 
                    {
                        ("cache-control", "no-cache"),
                        ("dnt", "1"),
                        ("pragma", "no-cache"),
                        ("referer", $"{AppInit.conf.AnimeGo.host}/"),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    });

                    string content = player?.Value<string>("content");
                    if (string.IsNullOrWhiteSpace(content))
                        return Content(string.Empty);

                    var g = Regex.Match(content, "data-player=\"(https?:)?//(aniboom\\.[^/]+)/embed/([^\"\\?&]+)\\?episode=1\\&amp;translation=([0-9]+)\"").Groups;
                    if (string.IsNullOrWhiteSpace(g[2].Value) || string.IsNullOrWhiteSpace(g[3].Value) || string.IsNullOrWhiteSpace(g[4].Value))
                        return Content(string.Empty);

                    links = new List<(string episode, string uri)>();
                    var match = Regex.Match(content, "data-episode=\"([0-9]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value))
                            links.Add((match.Groups[1].Value, $"video.m3u8?host={g[2].Value}&token={g[3].Value}&t={g[4].Value}&e={match.Groups[1].Value}"));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return Content(string.Empty);

                    memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                }

                foreach (var l in links)
                {
                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + l.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + $"{AppInit.Host(HttpContext)}/lite/animego/{l.uri}" + "\",\"title\":\"" + $"{title} ({l.episode} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{l.episode} серия" + "</div></div>";
                    firstjson = true;
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region Video
        [HttpGet]
        [Route("lite/animego/video.m3u8")]
        async public Task<ActionResult> Video(string host, string token, int t, int e)
        {
            if (!AppInit.conf.AnimeGo.enable)
                return Content(string.Empty);

            string memKey = $"animego:video:{token}:{t}:{e}";
            if (!memoryCache.TryGetValue(memKey, out string hls))
            {
                string embed = await HttpClient.Get($"https://{host}/embed/{token}?episode={e}&translation={t}", timeoutSeconds: 10, useproxy: AppInit.conf.AnimeGo.useproxy, addHeaders: new List<(string name, string val)>()
                {
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("referer", $"{AppInit.conf.AnimeGo.host}/"),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-requested-with", "XMLHttpRequest")
                });

                if (string.IsNullOrWhiteSpace(embed))
                    return Content(string.Empty);

                embed = embed.Replace("&quot;", "\"").Replace("\\", "");

                hls = Regex.Match(embed, "\"hls\":\"\\{\"src\":\"(https?:)?(//[^\"]+\\.m3u8)\"").Groups[2].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return Content(string.Empty);

                hls = "https:" + hls;
                memoryCache.Set(memKey, hls, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
            }

            return Redirect($"{AppInit.Host(HttpContext)}/proxy/{hls}");
        }
        #endregion
    }
}
