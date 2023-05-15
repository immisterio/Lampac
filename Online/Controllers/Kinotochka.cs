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
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class Kinotochka : BaseController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(string title, int year, int serial, string newsuri, int s = -1)
        {
            if (!AppInit.conf.Kinotochka.enable || string.IsNullOrWhiteSpace(title))
                return Content(string.Empty);

            var proxyManager = new ProxyManager("kinotochka", AppInit.conf.Kinotochka);
            var proxy = proxyManager.Get();

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
                        string search = await HttpClient.Post($"{AppInit.conf.Kinotochka.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                        if (search == null)
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

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
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

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
                        string news = await HttpClient.Get(newsuri, timeoutSeconds: 8, proxy: proxy);
                        string filetxt = Regex.Match(news ?? "", "file:\"(https?://[^\"]+\\.txt)\"").Groups[1].Value;

                        if (string.IsNullOrWhiteSpace(filetxt))
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

                        var root = await HttpClient.Get<JObject>(filetxt, timeoutSeconds: 8, proxy: proxy);
                        if (root == null)
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

                        var playlist = root.Value<JArray>("playlist");
                        if (playlist == null)
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

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
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

                        memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                    }

                    foreach (var l in links)
                    {
                        string link = HostStreamProxy(true, l.uri);
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(l.name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.name})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + l.name + "</div></div>";
                        firstjson = true;
                    }
                    #endregion
                }
            }
            else
            {
                #region Фильм
                string memKey = $"kinotochka:view:{title}:{year}";
                if (!memoryCache.TryGetValue(memKey, out string file))
                {
                    string search = await HttpClient.Post($"{AppInit.conf.Kinotochka.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                    if (search == null)
                    {
                        proxyManager.Refresh();
                        return Content(string.Empty);
                    }

                    string link = null, reservedlink = null;
                    foreach (string row in search.Split("sres-wrap clearfix").Skip(1))
                    {
                        var g = Regex.Match(row, "<h2>([^\\(]+) \\(([0-9]{4})\\)</h2>").Groups;

                        if (g[1].Value.ToLower().Trim() == title.ToLower())
                        {
                            reservedlink = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                            if (string.IsNullOrWhiteSpace(reservedlink))
                                continue;

                            if (g[2].Value == year.ToString())
                            {
                                link = reservedlink;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(link))
                    {
                        if (string.IsNullOrWhiteSpace(reservedlink))
                        {
                            proxyManager.Refresh();
                            return Content(string.Empty);
                        }

                        link = reservedlink;
                    }

                    string news = await HttpClient.Get(link, timeoutSeconds: 8, proxy: proxy);
                    file = Regex.Match(news ?? "", "id:\"playerjshd\", file:\"(https?://[^\"]+)\"").Groups[1].Value;

                    if (string.IsNullOrWhiteSpace(file))
                    {
                        proxyManager.Refresh();
                        return Content(string.Empty);
                    }

                    foreach (string f in file.Split(",").Reverse())
                    {
                        if (string.IsNullOrWhiteSpace(f))
                            continue;

                        file = f;
                        break;
                    }

                    memoryCache.Set(memKey, file, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                }

                file = HostStreamProxy(true, file);
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
