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

namespace Lampac.Controllers.LITE
{
    public class Animebesst : BaseController
    {
        [HttpGet]
        [Route("lite/animebesst")]
        async public Task<ActionResult> Index(string title, string uri, int s)
        {
            if (!AppInit.conf.Animebesst.enable || string.IsNullOrWhiteSpace(title))
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                string memkey = $"animebesst:search:{title}";
                if (!memoryCache.TryGetValue(memkey, out List<(string title, string uri, string s)> catalog))
                {
                    string search = await HttpClient.Post($"{AppInit.conf.Animebesst.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, useproxy: AppInit.conf.Animebesst.useproxy);
                    if (search == null)
                        return Content(string.Empty);

                    catalog = new List<(string title, string uri, string s)>();

                    foreach (string row in search.Split("class=\"shortstory\"").Skip(1))
                    {
                        var g = Regex.Match(row, "<h2><a href=\"(https?://[^\"]+\\.html)\"[^>]+>([^<]+)</a></h2>").Groups;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                        {
                            string season = "0";
                            if (!g[2].Value.Contains("сезон") || g[2].Value.Contains("1 сезон"))
                                season = "1";

                            if (g[2].Value.ToLower().StartsWith(title.ToLower()))
                                catalog.Add((g[2].Value, g[1].Value, season));
                        }
                    }

                    if (catalog.Count == 0)
                        return Content(string.Empty);

                    memoryCache.Set(memkey, catalog, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
                }

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animebesst?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog[0].uri)}&s={catalog[0].s}");

                foreach (var res in catalog)
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/animebesst?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + res.title + "</div></div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animebesst:playlist:{uri}";
                if (!memoryCache.TryGetValue(memKey, out List<(string episode, string uri)> links))
                {
                    string news = await HttpClient.Get(uri, timeoutSeconds: 10, useproxy: AppInit.conf.Animebesst.useproxy);
                    if (string.IsNullOrWhiteSpace(news))
                        return Content(string.Empty);

                    string videoList = Regex.Match(news, "var videoList = ([^\n\r]+)").Groups[1].Value.Replace("\\", "");
                    if (string.IsNullOrWhiteSpace(videoList))
                        return Content(string.Empty);

                    links = new List<(string episode, string uri)>();
                    var match = Regex.Match(videoList, "\"id\":\"([0-9]+)\",\"link\":\"(https?:)?//([^\"]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[3].Value))
                            links.Add((match.Groups[1].Value, match.Groups[3].Value));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return Content(string.Empty);

                    memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                }

                foreach (var l in links)
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/animebesst/video.m3u8?uri={HttpUtility.UrlEncode(l.uri)}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + l.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.episode} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{l.episode} серия" + "</div></div>";
                    firstjson = true;
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region Video
        [HttpGet]
        [Route("lite/animebesst/video.m3u8")]
        async public Task<ActionResult> Video(string uri)
        {
            if (!AppInit.conf.Animebesst.enable)
                return Content(string.Empty);

            string memKey = $"animebesst:video:{uri}";
            if (!memoryCache.TryGetValue(memKey, out string hls))
            {
                string iframe = await HttpClient.Get($"https://{uri}", timeoutSeconds: 8, useproxy: AppInit.conf.Animebesst.useproxy);
                if (string.IsNullOrWhiteSpace(iframe))
                    return Content(string.Empty);

                hls = Regex.Match(iframe, "file:\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return Content(string.Empty);

                memoryCache.Set(memKey, hls, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
            }

            return Redirect($"{AppInit.Host(HttpContext)}/proxy/{hls}");
        }
        #endregion
    }
}
