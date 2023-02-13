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
using Lampac.Models.LITE.Ashdi;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class Eneyida : BaseController
    {
        [HttpGet]
        [Route("lite/eneyida")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, string original_language, int year, int t = -1, int s = -1, string href = null)
        {
            if (!AppInit.conf.Eneyida.enable)
                return Content(string.Empty);

            if (original_language != "en")
                clarification = 1;

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region embed
            var result = await embed(clarification == 1 ? title : original_title, year, href);
            if (result.content == null)
            {
                if (string.IsNullOrWhiteSpace(href) && result.similar != null && result.similar.Count > 0)
                {
                    foreach (var similar in result.similar)
                    {
                        string link = $"{host}/lite/eneyida?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&href={HttpUtility.UrlEncode(similar.href)}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + similar.title + "</div></div></div>";
                        firstjson = false;
                    }

                    return Content(html + "</div>", "text/html; charset=utf-8");
                }

                return Content(string.Empty);
            }
            #endregion

            if (!result.content.Contains("file:'[{"))
            {
                #region Фильм
                string hls = Regex.Match(result.content, "file:\"(https?://[^\"]+/index.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return Content(string.Empty);

                #region subtitle
                string subtitles = string.Empty;

                string subtitle = new Regex("\"subtitle\":\"([^\"]+)\"").Match(result.content).Groups[1].Value;
                if (!string.IsNullOrWhiteSpace(subtitle))
                {
                    var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(subtitle);
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                        {
                            string suburl = HostStreamProxy(AppInit.conf.Eneyida.streamproxy, match.Groups[2].Value);
                            subtitles += "{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},";
                        }

                        match = match.NextMatch();
                    }
                }

                subtitles = Regex.Replace(subtitles, ",$", "");
                #endregion

                hls = HostStreamProxy(AppInit.conf.Eneyida.streamproxy, hls);
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";
                #endregion
            }
            else
            {
                #region Сериал
                try
                {
                    var root = JsonConvert.DeserializeObject<List<Voice>>(new Regex("file:'([^\n\r]+)',").Match(result.content).Groups[1].Value);

                    if (s == -1)
                    {
                        foreach (var voice in root)
                        {
                            foreach (var season in voice.folder)
                            {
                                if (html.Contains(season.title))
                                    continue;

                                string numberseason = Regex.Match(season.title, "([0-9]+)$").Groups[1].Value;
                                string link = $"{host}/lite/eneyida?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&href={HttpUtility.UrlEncode(href)}&s={numberseason}";

                                html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season.title + "</div></div></div>";
                                firstjson = false;
                            }
                        }
                    }
                    else
                    {
                        #region Перевод
                        for (int i = 0; i < root.Count; i++)
                        {
                            if (root[i].folder.FirstOrDefault(i => i.title.EndsWith($" {s}")) == null)
                                continue;

                            if (t == -1)
                                t = i;

                            string link = $"{host}/lite/eneyida?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}&href={HttpUtility.UrlEncode(href)}&s={s}&t={i}";

                            html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + root[i].title + "</div>";
                        }

                        html += "</div><div class=\"videos__line\">";
                        #endregion

                        foreach (var episode in root[t].folder.First(i => i.title.EndsWith($" {s}")).folder)
                        {
                            #region subtitle
                            string subtitles = string.Empty;

                            if (!string.IsNullOrWhiteSpace(episode.subtitle))
                            {
                                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,]+)").Match(episode.subtitle);
                                while (match.Success)
                                {
                                    if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                                    {
                                        string suburl = HostStreamProxy(AppInit.conf.Eneyida.streamproxy, match.Groups[2].Value);
                                        subtitles += "{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},";
                                    }

                                    match = match.NextMatch();
                                }
                            }

                            subtitles = Regex.Replace(subtitles, ",$", "");
                            #endregion

                            string file = HostStreamProxy(AppInit.conf.Eneyida.streamproxy, episode.file);
                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(episode.title, "([0-9]+)$").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + $"{title ?? original_title} ({episode.title})" + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + episode.title + "</div></div>";
                            firstjson = false;
                        }
                    }
                }
                catch 
                {
                    return Content(string.Empty);
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async ValueTask<(string content, List<(string title, string href)> similar)> embed(string original_title, int year, string href)
        {
            if (string.IsNullOrWhiteSpace(href) && (string.IsNullOrWhiteSpace(original_title) || year == 0))
                return (null, null);

            string memKey = $"eneyida:view:{original_title}:{year}:{href}";

            if (!memoryCache.TryGetValue(memKey, out (string content, List<(string title, string href)> similar) result))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Eneyida.useproxy)
                    proxy = HttpClient.webProxy();

                string link = href, reservedlink = null;

                if (string.IsNullOrWhiteSpace(link))
                {
                    string search = await HttpClient.Post($"{AppInit.conf.Eneyida.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&result_from=1&story={HttpUtility.UrlEncode(original_title)}", timeoutSeconds: 8, proxy: proxy);
                    if (search == null)
                        return (null, null);

                    foreach (string row in search.Split("<article ").Skip(1))
                    {
                        if (row.Contains(">Анонс</div>") || row.Contains(">Трейлер</div>"))
                            continue;

                        string newslink = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(newslink))
                            continue;

                        var g = Regex.Match(row, "class=\"short_subtitle\"><a [^>]+>([0-9]{4})</a> &bull; ([^<]+)</div>").Groups;

                        string name = g[2].Value.ToLower().Trim();
                        if (result.similar == null)
                            result.similar = new List<(string title, string href)>();

                        result.similar.Add(($"{name} {g[1].Value}", newslink));

                        if (name == original_title.ToLower())
                        {
                            if (g[1].Value == year.ToString())
                            {
                                reservedlink = newslink;
                                link = reservedlink;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(link))
                    {
                        if (string.IsNullOrWhiteSpace(reservedlink))
                            return (null, result.similar);

                        link = reservedlink;
                    }
                }

                string news = await HttpClient.Get(link, timeoutSeconds: 8, proxy: proxy);
                if (news == null)
                    return (null, null);

                string iframeUri = Regex.Match(news, "<iframe width=\"100%\" height=\"400\" src=\"(https?://[^/]+/[^\"]+/[0-9]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(iframeUri))
                    return (null, null);

                result.content = await HttpClient.Get(iframeUri, timeoutSeconds: 8, proxy: proxy);
                if (result.content == null || !result.content.Contains("file:"))
                    return (null, null);

                memoryCache.Set(memKey, result, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return result;
        }
        #endregion
    }
}
