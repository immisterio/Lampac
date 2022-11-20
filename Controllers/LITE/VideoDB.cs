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

namespace Lampac.Controllers.LITE
{
    public class VideoDB : BaseController
    {
        [HttpGet]
        [Route("lite/videodb")]
        async public Task<ActionResult> Index(int id, long kinopoisk_id, string title, string original_title, int serial, string t, int s = -1, int sid = -1)
        {
            if (kinopoisk_id == 0 || id == 0 || !AppInit.conf.VideoDB.enable)
                return Content(string.Empty);

            var root = await embed(kinopoisk_id, serial);
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

                    #region streansquality
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var quality in new List<string> { "2160", "2060", "1440", "1080", "720", "480", "360", "240" })
                    {
                        string link = new Regex($"\\[{quality}p?\\]" + "([^\\[\\|,\n\r\t ]+\\.(mp4|m3u8))").Match(file).Groups[1].Value;
                        if (string.IsNullOrEmpty(link))
                            continue;

                        link = AppInit.conf.VideoDB.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link;

                        streams.Add((link, $"{quality}p"));
                        streansquality += $"\"{quality}p\":\"" + link + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                    #endregion

                    #region subtitle
                    string subtitles = string.Empty;

                    try
                    {
                        int subx = 1;
                        foreach (string cc in pl.Value<string>("subtitle").Split(","))
                        {
                            if (string.IsNullOrWhiteSpace(cc) || !cc.EndsWith(".srt"))
                                continue;

                            string suburl = AppInit.conf.VideoDB.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{cc}" : cc;
                            subtitles += "{\"label\": \"" + $"sub #{subx}" + "\",\"url\": \"" + suburl + "\"},";
                            subx++;
                        }
                    }
                    catch { }

                    subtitles = Regex.Replace(subtitles, ",$", "");
                    #endregion


                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + ", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    for (int i = 0; i < root.pl.Count; i++)
                    {
                        string season = Regex.Match(root.pl[i].Value<string>("title"), "^([0-9]+)").Groups[1].Value;
                        string link = $"{AppInit.Host(HttpContext)}/lite/videodb?id={id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season}&sid={i}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + root.pl[i].Value<string>("title") + "</div></div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    var season = root.pl[sid].Value<JArray>("folder");

                    #region Перевод
                    foreach (var episode in season)
                    {
                        foreach (var pl in episode.Value<JArray>("folder"))
                        {
                            string perevod = pl.Value<string>("comment");

                            if (html.Contains(perevod))
                                continue;

                            if (string.IsNullOrWhiteSpace(t))
                                t = perevod;

                            string link = $"{AppInit.Host(HttpContext)}/lite/videodb?id={id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&sid={sid}&t={HttpUtility.UrlEncode(perevod)}";
                            string active = t == perevod ? "active" : "";

                            html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + perevod + "</div>";
                        }
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    #region Серии
                    foreach (var episode in season)
                    {
                        foreach (var pl in episode.Value<JArray>("folder"))
                        {
                            string perevod = pl.Value<string>("comment");
                            if (perevod != t)
                                continue;

                            string name = episode.Value<string>("title");
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

                                link = AppInit.conf.VideoDB.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link;

                                streams.Add((link, $"{quality}p"));
                                streansquality += $"\"{quality}p\":\"" + link + "\",";
                            }

                            streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({name})" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                            firstjson = false;
                        }
                    }
                    #endregion
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async ValueTask<(JArray pl, bool movie)> embed(long kinopoisk_id, int serial)
        {
            string memKey = $"videodb:view:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out (JArray pl, bool movie) cache))
            {
                string host = "https://kinoplay.site";
                string html = await HttpClient.Get($"{host}/iplayer/videodb.php?kp={kinopoisk_id}" + (serial > 0 ? "&series=true" : ""), timeoutSeconds: 8, useproxy: AppInit.conf.VideoDB.useproxy, addHeaders: new List<(string name, string val)>()
                {
                    ("cache-control", "no-cache"),
                    ("cookie", "invite=a246a3f46c82fe439a45c3dbbbb24ad5"),
                    ("sec-fetch-dest", "iframe"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "same-origin"),
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("referer", $"{host}/"),
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
    }
}
