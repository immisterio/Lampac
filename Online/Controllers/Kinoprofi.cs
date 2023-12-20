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
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class Kinoprofi : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/kinoprofi")]
        async public Task<ActionResult> Index(string title, int year, int serial, string newsuri, string session, int s = -1)
        {
            var init = AppInit.conf.Kinoprofi;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager("kinoprofi", init);
            var proxy = proxyManager.Get();

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (serial == 1)
            {
                if (s == -1)
                {
                    #region Сезоны
                    string memKey = $"kinoprofi:seasons:{title}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string search = await HttpClient.Get($"{init.corsHost()}/search/f:{HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                        string session_id = Regex.Match(search ?? "", "session_id += '([^']+)'").Groups[1].Value;

                        if (string.IsNullOrWhiteSpace(session_id))
                            return OnError(proxyManager);

                        links = new List<(string, string)>();

                        foreach (string row in Regex.Replace(search, "[\n\r\t]+", "").Split("sh-block").Skip(1).Reverse())
                        {
                            var g = Regex.Match(row, "<a href=\"(https?://[^\"]+\\.html)\" itemprop=\"url\">([^<]+) \\((([0-9]+) сезон)\\)", RegexOptions.IgnoreCase).Groups;
                            if (g[2].Value.Trim().ToLower() == title.ToLower())
                            {
                                if (string.IsNullOrWhiteSpace(g[1].Value))
                                    continue;

                                links.Add((g[3].Value.ToLower(), $"{host}/lite/kinoprofi?title={HttpUtility.UrlEncode(title)}&serial={serial}&s={g[4].Value}&newsuri={HttpUtility.UrlEncode(g[1].Value)}&session={session_id}"));
                            }
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, cacheTime(30));
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
                    string memKey = $"kinoprofi:playlist:{newsuri}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string newsid = Regex.Match(newsuri, "https?://[^/]+/([0-9]+)-").Groups[1].Value;
                        if (string.IsNullOrEmpty(newsid))
                            return OnError(proxyManager);

                        var root = await HttpClient.Post<JObject>($"{init.apihost}/getplay", $"key%5Bid%5D={newsid}&pl_type=movie&session={session}&is_mobile=0&dle_group=5", timeoutSeconds: 8, proxy: proxy);
                        if (root == null)
                            return OnError(proxyManager);

                        var playlist = root.Value<JObject>("pl")?.Value<JObject>("hls")?.Value<JArray>("playlist");
                        if (playlist == null)
                            return OnError(proxyManager);

                        links = new List<(string name, string uri)>();

                        foreach (var pl in playlist)
                        {
                            string name = pl.Value<string>("comment");
                            string file = pl.Value<string>("file");
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(file))
                            {
                                if (name.StartsWith("<"))
                                    name = Regex.Match(name, "<b>([^<\"]+)").Groups[1].Value;

                                links.Add((name, file));
                            }
                        }

                        if (links.Count == 0)
                            return OnError(proxyManager);

                        memoryCache.Set(memKey, links, cacheTime(30));
                    }

                    foreach (var l in links)
                    {
                        string link = HostStreamProxy(init, l.uri, new List<(string, string)>() { ("referer", init.host) }, proxy: proxy);
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + Regex.Match(l.name, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} ({l.name})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + l.name + "</div></div>";
                        firstjson = true;
                    }
                    #endregion
                }
            }
            else
            {
                #region Фильм
                string memKey = $"kinoprofi:view:{title}:{year}";
                if (!memoryCache.TryGetValue(memKey, out string file))
                {
                    string keyid = null, reservedlink = null;
                    string search = await HttpClient.Get($"{init.corsHost()}/search/f:{HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);

                    foreach (string row in Regex.Replace(search ?? "", "[\n\r\t]+", "").Split("sh-block").Skip(1))
                    {
                        if (Regex.Match(row, "itemprop=\"name\" content=\"([^\"]+)\"").Groups[1].Value.ToLower() != title.ToLower())
                            continue;

                        reservedlink = Regex.Match(row, "href=\"https?://[^/]+/([0-9]+)-[^\"]+\" itemprop=\"url\"").Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(reservedlink))
                            continue;

                        if (Regex.Match(row, "<b>Год</b> ?<i>([0-9]{4})</i>").Groups[1].Value == year.ToString())
                        {
                            keyid = reservedlink;
                            break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(keyid))
                    {
                        if (string.IsNullOrWhiteSpace(reservedlink))
                            return OnError(proxyManager);

                        keyid = reservedlink;
                    }

                    string session_id = Regex.Match(search ?? "", "var session_id += '([^']+)'").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(session_id))
                        return OnError(proxyManager);

                    string json = await HttpClient.Post($"{init.apihost}/getplay", $"key%5Bid%5D={keyid}&pl_type=movie&session={session_id}&is_mobile=0&dle_group=5", timeoutSeconds: 8, proxy: proxy);
                    if (json == null || !json.Contains(".m3u8"))
                        return OnError(proxyManager);

                    file = Regex.Match(json, "\"hls\":\"(https?:[^\"]+)\"").Groups[1].Value.Replace("\\", "");
                    if (string.IsNullOrWhiteSpace(file))
                        return OnError(proxyManager);

                    memoryCache.Set(memKey, file, cacheTime(40));
                }

                file = HostStreamProxy(init, file, new List<(string, string)>() { ("referer", init.host) }, proxy: proxy);
                return Content(new MovieTpl(title).ToHtml("По умолчанию", file), "text/html; charset=utf-8");
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
