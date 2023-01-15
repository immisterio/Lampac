using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.CDNmovies;

namespace Lampac.Controllers.LITE
{
    public class CDNmovies : BaseController
    {
        [HttpGet]
        [Route("lite/cdnmovies")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t, int s = -1, int sid = -1)
        {
            if (!AppInit.conf.CDNmovies.enable || kinopoisk_id == 0)
                return Content(string.Empty);

            var voices = await embed(kinopoisk_id);
            if (voices == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region Перевод html
            for (int i = 0; i < voices.Count; i++)
            {
                string link = $"{host}/lite/cdnmovies?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={i}";

                html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + voices[i].title + "</div>";
            }

            html += "</div><div class=\"videos__line\">";
            #endregion

            if (s == -1)
            {
                #region Сезоны
                for (int i = 0; i < voices[t].folder.Count; i++)
                {
                    string season = Regex.Match(voices[t].folder[i].title, "([0-9]+)$").Groups[1].Value;
                    string link = $"{host}/lite/cdnmovies?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={t}&s={season}&sid={i}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season} сезон" + "</div></div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Серии
                foreach (var item in voices[t].folder[sid].folder)
                {
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var quality in new List<string> { "720", "480", "360", "240" })
                    {
                        string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(item.file).Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        link = Regex.Replace(link, "^https?://[^/]+", "https://s1.cdnmovies.nl");
                        link = HostStreamProxy(AppInit.conf.CDNmovies.streamproxy, link);

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    string episode = Regex.Match(item.title, "([0-9]+)$").Groups[1].Value;
                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode} cерия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode} cерия" + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async ValueTask<List<Voice>> embed(long kinopoisk_id)
        {
            string memKey = $"cdnmovies:view:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out List<Voice> cache))
            {
                string html = await HttpClient.Get($"{AppInit.conf.CDNmovies.host}/serial/kinopoisk/{kinopoisk_id}", timeoutSeconds: 8, useproxy: AppInit.conf.CDNmovies.useproxy, addHeaders: new List<(string name, string val)>()
                {
                    ("DNT", "1"),
                    ("Upgrade-Insecure-Requests", "1")
                });

                if (html == null)
                    return null;

                string file = Regex.Match(html, "file:'([^\n\r]+)'").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(file))
                    return null;

                try
                {
                    cache = JsonConvert.DeserializeObject<List<Voice>>(file);
                }
                catch { return null; }

                if (cache == null || cache.Count == 0)
                    return null;

                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }

            return cache;
        }
        #endregion
    }
}
