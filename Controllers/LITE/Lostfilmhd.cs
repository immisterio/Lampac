using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lampac.Controllers.LITE
{
    public class Lostfilmhd : BaseController
    {
        [HttpGet]
        [Route("lite/lostfilmhd")]
        async public Task<ActionResult> Index(string title, int year, int s = -1)
        {
            if (!AppInit.conf.Lostfilmhd.enable)
                return Content(string.Empty);

            if (year == 0 || string.IsNullOrWhiteSpace(title))
                return Content(string.Empty);

            var content = await embed(title, year);
            if (content.seasons == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (s == -1)
            {
                foreach (var season in content.seasons)
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/lostfilmhd?title={HttpUtility.UrlEncode(title)}&year={year}&s={season.Key}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
                    firstjson = false;
                }
            }
            else
            {
                if (content.seasons[s.ToString()] is JObject jb)
                {
                    foreach (var episode in jb.ToObject<Dictionary<string, Dictionary<string, object>>>())
                    {
                        string link = $"{AppInit.Host(HttpContext)}/lite/lostfilmhd/video?title={HttpUtility.UrlEncode(title)}&iframe={HttpUtility.UrlEncode(content.iframe_src)}&s={s}&e={episode.Key}&v={episode.Value.First().Key.Split('#')[0]}";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({episode.Key} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    return Content(string.Empty);
                }
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }

        #region Video
        [HttpGet]
        [Route("lite/lostfilmhd/video")]
        async public Task<ActionResult> Video(string iframe, int s, int e, int v, string title, string original_title)
        {
            if (!AppInit.conf.Lostfilmhd.enable)
                return Content(string.Empty);

            string memKey = $"lostfilmhd:view:{iframe}:{s}:{e}:{v}";
            if (!memoryCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await HttpClient.Get($"{iframe}?season={s}&episode={e}&voice={v}", referer: AppInit.conf.Lostfilmhd.host, useproxy: AppInit.conf.Lostfilmhd.useproxy, timeoutSeconds: 10);
                if (html == null)
                    return Content(string.Empty);

                urim3u8 = new Regex("\"hls\":\"(https?:[^\"]+\\.m3u8)\"").Match(html).Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(urim3u8))
                    return Content(string.Empty);

                memoryCache.Set(memKey, urim3u8, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 40 : 5));
            }

            return Content("{\"method\":\"play\",\"url\":\"" + $"{AppInit.Host(HttpContext)}/proxy/{urim3u8}" + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion


        #region embed
        async ValueTask<(Dictionary<string, object> seasons, string iframe_src)> embed(string title, int year)
        {
            string memKey = $"lostfilmhd:view:{title}:{year}";

            if (!memoryCache.TryGetValue(memKey, out (Dictionary<string, object> seasons, string iframe_src) cache))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Lostfilmhd.useproxy)
                    proxy = HttpClient.webProxy();

                string search = await HttpClient.Post($"{AppInit.conf.Lostfilmhd.host}/publ/", $"query={HttpUtility.UrlEncode(title)}&a=2", timeoutSeconds: 8, proxy: proxy);
                if (search == null)
                    return (null, null);

                string link = null;
                foreach (string row in search.Split("<div id=\"entryID").Skip(1))
                {
                    string href = Regex.Match(row, "href=\"/(publ/serialy/[^\"]+)\"").Groups[1].Value;
                    string vent = Regex.Match(row, "<strong>Выпущено</strong>: ([^\n\r<]+)").Groups[1].Value;
                    string eTitle = Regex.Match(row, "class=\"eTitle\"[^>]+><a [^>]+>([^<]+) ([0-9,\\-]+) сезон").Groups[1].Value;

                    if (vent.Contains(year.ToString()) && !string.IsNullOrWhiteSpace(href) && eTitle.ToLower().Trim() == title.ToLower())
                    {
                        link = href;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                    return (null, null);

                string news = await HttpClient.Get($"{AppInit.conf.Lostfilmhd.host}/{link}", timeoutSeconds: 8, proxy: proxy);
                if (news == null)
                    return (null, null);

                string iframe_src = new Regex("<iframe src=\"//([^/]+/pl/[0-9]+)\"").Match(news).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(iframe_src))
                    return (null, null);

                string iframe = await HttpClient.Get($"http://{iframe_src}", referer: AppInit.conf.Lostfilmhd.host, timeoutSeconds: 8, proxy: proxy);
                if (string.IsNullOrWhiteSpace(iframe) || iframe.Contains(">Контент недоступен в вашем регионе") || iframe.Contains("<title>404 Not Found</title>"))
                    return (null, null);

                string json = new Regex("data-voice=\"[^\"]+\"([^>]+)?>([^<]+)</div>").Match(iframe).Groups[2].Value;
                if (string.IsNullOrWhiteSpace(json))
                    return (null, null);

                cache.iframe_src = $"http://{iframe_src}";
                cache.seasons = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return cache;
        }
        #endregion
    }
}
