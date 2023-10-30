using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using Lampac.Engine.CORE;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class Kinotochka : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, int year, int serial, string newsuri, int s = -1)
        {
            if (!AppInit.conf.Kinotochka.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager("kinotochka", AppInit.conf.Kinotochka);
            var proxy = proxyManager.Get();

            // enable 720p
            string cookie = AppInit.conf.Kinotochka.cookie ??  "PHPSESSID=i9g8i9lr3j8d5tsrrm5bg4ush7; _ga=GA1.1.1814511493.1698667196; cf_clearance=bBUeH48bPNG6ilgXkfKc0DdVNWW9hEnn0l8wC9xHBKU-1698670014-0-1-ecb48842.7f2ed8fe.bbb5d7a2-150.0.0; dle_user_id=476720; dle_password=8842d3a884231414bdca85a8b9c00a0b; dle_newpm=0; _ga_6EPB4E046E=GS1.1.1698670015.2.1.1698670096.0.0.0";

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (serial == 1)
            {
                if (s == -1)
                {
                    #region Сезоны
                    string memKey = $"kinotochka:seasons:{title}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string search = await HttpClient.Post($"{AppInit.conf.Kinotochka.corsHost()}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                        if (search == null)
                            return OnError(proxyManager);

                        links = new List<(string, string)>();

                        foreach (string row in search.Split("sres-wrap clearfix").Skip(1).Reverse())
                        {
                            var gname = Regex.Match(row, "<h2>([^<]+) (([0-9]+) Сезон) \\([0-9]{4}\\)</h2>", RegexOptions.IgnoreCase).Groups;

                            if (gname[1].Value.ToLower() == title.ToLower())
                            {
                                string uri = Regex.Match(row, "href=\"(https?://[^\"]+\\.html)\"").Groups[1].Value;
                                if (string.IsNullOrWhiteSpace(uri))
                                    continue;

                                links.Add((gname[2].Value.ToLower(), $"{host}/lite/kinotochka?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={gname[3].Value}&newsuri={HttpUtility.UrlEncode(uri)}"));
                            }
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                    }

                    foreach (var l in links)
                    {
                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + l.uri + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + l.name + "</div></div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                else
                {
                    #region Серии
                    string memKey = $"kinotochka:playlist:{newsuri}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string news = await HttpClient.Get(newsuri, timeoutSeconds: 8, proxy: proxy, cookie: cookie);
                        string filetxt = Regex.Match(news ?? "", "file:\"(https?://[^\"]+\\.txt)\"").Groups[1].Value;

                        if (string.IsNullOrWhiteSpace(filetxt))
                            return OnError(proxyManager);

                        var root = await HttpClient.Get<JObject>(filetxt, timeoutSeconds: 8, proxy: proxy, cookie: cookie);
                        if (root == null)
                            return OnError(proxyManager);

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                            return OnError(proxyManager);

                        links = new List<(string name, string uri)>();

                        foreach (var pl in playlist)
                        {
                            string name = pl.Value<string>("comment");
                            string file = pl.Value<string>("file");
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(file))
                            {
                                if (file.Contains("].mp4"))
                                    file = Regex.Replace(file, "\\[[^\\]]+,([0-9]+)\\]\\.mp4$", "$1.mp4");

                                links.Add((name.Split("<")[0].Trim(), file));
                            }
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                    }

                    foreach (var l in links)
                    {
                        string link = HostStreamProxy(AppInit.conf.Kinotochka, l.uri, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(l.name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.name})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + l.name + "</div></div>";
                        firstjson = true;
                    }
                    #endregion
                }
            }
            else
            {
                #region Фильм
                if (kinopoisk_id == 0)
                    return OnError();

                string memKey = $"kinotochka:view:{kinopoisk_id}";
                if (!memoryCache.TryGetValue(memKey, out string file))
                {
                    string embed = await HttpClient.Get($"{AppInit.conf.Kinotochka.corsHost()}/embed/kinopoisk/{kinopoisk_id}", timeoutSeconds: 8, proxy: proxy, cookie: cookie);
                    file = Regex.Match(embed ?? "", "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;

                    if (string.IsNullOrWhiteSpace(file))
                        return OnError(proxyManager);

                    foreach (string f in file.Split(",").Reverse())
                    {
                        if (string.IsNullOrWhiteSpace(f))
                            continue;

                        file = f;
                        break;
                    }

                    memoryCache.Set(memKey, file, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                }

                file = HostStreamProxy(AppInit.conf.Kinotochka, file, proxy: proxy);
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
