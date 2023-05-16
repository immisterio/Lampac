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
    public class Kinokrad : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinokrad")]
        async public Task<ActionResult> Index(string title, int year, int serial, string newsuri, int s = -1)
        {
            if (!AppInit.conf.Kinokrad.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager("kinokrad", AppInit.conf.Kinokrad);
            var proxy = proxyManager.Get();

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (serial == 1)
            {
                if (s == -1)
                {
                    #region Сезоны
                    string memKey = $"kinokrad:seasons:{title}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string search = await HttpClient.Post($"{AppInit.conf.Kinokrad.host}/index.php?do=search", $"do=search&subaction=search&search_start=1&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                        if (search == null)
                            return OnError(proxyManager);

                        links = new List<(string, string)>();

                        foreach (string row in search.Split("searchitem").Skip(1).Reverse())
                        {
                            var g = Regex.Match(row, "<h3><a href=\"(https?://[^\"]+\\.html)\"([^>]+)?>([^<]+) \\((([0-9]+) сезон)\\)</a></h3>", RegexOptions.IgnoreCase).Groups;
                            if (g[3].Value.ToLower() == title.ToLower())
                            {
                                if (string.IsNullOrWhiteSpace(g[1].Value))
                                    continue;

                                links.Add((g[4].Value.ToLower(), $"{host}/lite/kinokrad?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={g[5].Value}&newsuri={HttpUtility.UrlEncode(g[1].Value)}"));
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
                    string memKey = $"kinokrad:playlist:{newsuri}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string news = await HttpClient.Get(newsuri, timeoutSeconds: 8, proxy: proxy);
                        string filetxt = Regex.Match(news ?? "", "\"/(playlist/[^\"]+\\.txt)\"").Groups[1].Value;

                        if (string.IsNullOrWhiteSpace(filetxt))
                            return OnError(proxyManager);

                        var root = await HttpClient.Get<JObject>($"{AppInit.conf.Kinokrad.host}/{filetxt}", timeoutSeconds: 8, proxy: proxy);
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
                                links.Add((name, file));
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                    }

                    foreach (var l in links)
                    {
                        string link = HostStreamProxy(AppInit.conf.Kinokrad, l.uri, new List<(string, string)>() { ("referer", AppInit.conf.Kinokrad.host) }, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(l.name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.name})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + l.name + "</div></div>";
                        firstjson = true;
                    }
                    #endregion
                }
            }
            else
            {
                #region Фильм
                string memKey = $"kinokrad:view:{title}:{year}";
                if (!memoryCache.TryGetValue(memKey, out string content))
                {
                    string search = await HttpClient.Post($"{AppInit.conf.Kinokrad.host}/index.php?do=search", $"do=search&subaction=search&search_start=1&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                    if (search == null)
                        return OnError(proxyManager);

                    string link = null, reservedlink = null;
                    foreach (string row in search.Split("searchitem").Skip(1))
                    {
                        var g = Regex.Match(row, "<h3><a href=\"(https?://[^/]+/[^\"]+\\.html)\"([^>]+)?>([^\\(]+) \\(([0-9]{4})\\)</a></h3>").Groups;

                        if (g[3].Value.ToLower().Trim() == title.ToLower())
                        {
                            reservedlink = g[1].Value;
                            if (string.IsNullOrWhiteSpace(reservedlink))
                                continue;

                            if (g[4].Value == year.ToString())
                            {
                                link = reservedlink;
                                break;
                            }
                        }
                    }

                    if (string.IsNullOrWhiteSpace(link))
                    {
                        if (string.IsNullOrWhiteSpace(reservedlink))
                            return OnError(proxyManager);

                        link = reservedlink;
                    }

                    string news = await HttpClient.Get(link, timeoutSeconds: 8, proxy: proxy);
                    content = Regex.Match(news ?? "", "player1-link-movie([^\n\r]+)").Groups[1].Value;

                    if (string.IsNullOrWhiteSpace(content))
                        return OnError(proxyManager);

                    memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
                }

                foreach (var quality in new List<string> { "1080", "720", "480", "360", "240" })
                {
                    string hls = new Regex($"\\[{quality}p\\]" + "(https?://[^\\[\\|\",;\n\r\t ]+.m3u8)").Match(content).Groups[1].Value;
                    if (!string.IsNullOrEmpty(hls))
                    {
                        hls = HostStreamProxy(AppInit.conf.Kinokrad, hls, new List<(string, string)>() { ("referer", AppInit.conf.Kinokrad.host) }, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + quality + "p</div></div>";
                        firstjson = true;
                    }
                }

                if (html == "<div class=\"videos__line\">")
                    return OnError(proxyManager);
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
