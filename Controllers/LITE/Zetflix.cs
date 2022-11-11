using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class Zetflix : BaseController
    {
        [HttpGet]
        [Route("lite/zetflix")]
        async public Task<ActionResult> Index(int id, long kinopoisk_id, string title, string original_title, string t, int s = -1)
        {
            if (kinopoisk_id == 0 || id == 0 || !AppInit.conf.Zetflix.enable)
                return Content(string.Empty);

            var root = await embed(kinopoisk_id, s);
            if (root.pl == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (root.movie)
            {
                #region Фильм
                foreach (var pl in root.pl)
                {
                    string name = pl.Value<string>("title");
                    string file = pl.Value<string>("file");

                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                        continue;

                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var quality in new List<string> { "2160", "2060", "1440", "1080", "720", "480", "360", "240" })
                    {
                        string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(file).Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        link = AppInit.conf.Zetflix.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link;

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    int number_of_seasons = 1;

                    var themoviedb = await HttpClient.Get<JObject>($"https://api.themoviedb.org/3/tv/{id}?api_key=4ef0d7355d9ffb5151e987764708ce96", timeoutSeconds: 8);
                    if (themoviedb != null)
                    {
                        number_of_seasons = themoviedb.Value<int>("number_of_seasons");
                        if (1 > number_of_seasons)
                            number_of_seasons = 1;
                    }

                    for (int i = 1; i <= number_of_seasons; i++)
                    {
                        string link = $"{AppInit.Host(HttpContext)}/lite/zetflix?id={id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={i}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{i} сезон" + "</div></div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    #region Перевод
                    foreach (var pl in root.pl)
                    {
                        string perevod = pl.Value<string>("title");

                        if (html.Contains(perevod))
                            continue;

                        if (string.IsNullOrWhiteSpace(t))
                            t = perevod;

                        string link = $"{AppInit.Host(HttpContext)}/lite/zetflix?id={id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={HttpUtility.UrlEncode(perevod)}";
                        string active = t == perevod ? "active" : "";

                        html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + perevod + "</div>";
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    #region Серии
                    foreach (var pl in root.pl.Reverse())
                    {
                        string perevod = pl.Value<string>("title");
                        if (perevod != t)
                            continue;

                        var item = pl.Value<JArray>("folder").First;
                        string name = item.Value<string>("comment");
                        string file = item.Value<string>("file");

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(file))
                            continue;

                        string streansquality = string.Empty;
                        List<(string link, string quality)> streams = new List<(string, string)>();

                        foreach (var quality in new List<string> { "2160", "2060", "1440", "1080", "720", "480", "360", "240" })
                        {
                            string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(file).Groups[1].Value;
                            if (string.IsNullOrEmpty(link))
                                continue;

                            link = AppInit.conf.Zetflix.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link;

                            streams.Add((link, $"{quality}p"));
                            streansquality += $"\"{quality}p\":\"" + link + "\",";
                        }

                        streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async ValueTask<(JArray pl, bool movie)> embed(long kinopoisk_id, int s)
        {
            string memKey = $"zetfix:view:{kinopoisk_id}:{s}";

            if (!memoryCache.TryGetValue(memKey, out (JArray pl, bool movie) cache))
            {
                string host = await getHost();
                string html = await HttpClient.Get($"{host}/iplayer/videodb.php?kp={kinopoisk_id}" + (s > 0 ? $"&season={s}" : ""), timeoutSeconds: 8, useproxy: AppInit.conf.Zetflix.useproxy, addHeaders: new List<(string name, string val)>()
                {
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("referer", $"{host}/iplayer/player.php?id=JTJGaXBsYXllciUyRnZpZGVvZGIucGhwJTNGa3AlM0Q0NDcxMDU0JTI2c2Vhc29uJTNEMSUyNmVwaXNvZGUlM0Q2JTI2cG9zdGVyJTNEaHR0cHMlM0ElMkYlMkZ6ZXRmaXgub25saW5lJTJGdXBsb2FkcyUyRnBvc3RzJTJGMjAyMS0wNyUyRjE2MjUxMjY0NTVfbW9uYXJoaTYuanBnJTI2enZ1ayUzRFNESStNZWRpYSUyQ1ZTSStNb3Njb3clMkMlRDAlOUYlRDAlQjglRDElODQlRDAlQjAlRDAlQjMlRDAlQkUlRDElODAlMkNOZXRmbGl4JTJDTG9zdEZpbG0=&poster=aHR0cHM6Ly96ZXRmaXgub25saW5lL3VwbG9hZHMvcG9zdHMvMjAyMS0wNy8xNjI1MTI2NDU1X21vbmFyaGk2LmpwZw=="),
                    ("upgrade-insecure-requests", "1")
                });

                string file = new Regex("file:([^\n\r]+,\\])").Match(html).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(file))
                    return (null, false);

                cache.movie = !file.Contains("\"comment\":");
                cache.pl = JsonConvert.DeserializeObject<JArray>(file);
                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            return cache;
        }
        #endregion

        #region getHost
        async ValueTask<string> getHost()
        {
            string memKey = "zetfix:getHost";

            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                location = await HttpClient.GetLocation(AppInit.conf.Zetflix.host, timeoutSeconds: 8, referer: "https://www.google.com/");

                if (string.IsNullOrWhiteSpace(location))
                    return AppInit.conf.Zetflix.host;

                location = Regex.Match(location, "^(https?://[^/]+)").Groups[1].Value;
                memoryCache.Set(memKey, location, DateTime.Now.AddMinutes(20));
            }

            return location;
        }
        #endregion
    }
}
