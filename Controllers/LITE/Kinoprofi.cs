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

namespace Lampac.Controllers.LITE
{
    public class Kinoprofi : BaseController
    {
        [HttpGet]
        [Route("lite/kinoprofi")]
        async public Task<ActionResult> Index(string title, int year, int serial, string newsuri, string session, int s = -1)
        {
            if (string.IsNullOrWhiteSpace(title) || !AppInit.conf.Kinoprofi.enable)
                return Content(string.Empty);

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
                        string search = await HttpClient.Get($"{AppInit.conf.Kinoprofi.host}/search/f:{HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, useproxy: AppInit.conf.Kinoprofi.useproxy);
                        if (search == null)
                            return Content(string.Empty);

                        string session_id = Regex.Match(search, "session_id += '([^']+)'").Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(session_id))
                            return Content(string.Empty);

                        links = new List<(string, string)>();

                        foreach (string row in Regex.Replace(search, "[\n\r\t]+", "").Split("sh-block ns").Skip(1).Reverse())
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
                            return Content(string.Empty);

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
                    string memKey = $"kinoprofi:playlist:{newsuri}";
                    if (!memoryCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        string newsid = Regex.Match(newsuri, "https?://[^/]+/([0-9]+)-").Groups[1].Value;

                        var root = await HttpClient.Post<JObject>($"{AppInit.conf.Kinoprofi.apihost}/getplay", $"key%5Bid%5D={newsid}&pl_type=movie&session={session}&is_mobile=0&dle_group=5", timeoutSeconds: 8, useproxy: AppInit.conf.Kinoprofi.useproxy);
                        if (root == null)
                            return Content(string.Empty);

                        var playlist = root.Value<JObject>("pl")?.Value<JObject>("hls")?.Value<JArray>("playlist");
                        if (playlist == null)
                            return Content(string.Empty);

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
                            return Content(string.Empty);

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
                string memKey = $"kinoprofi:view:{title}:{year}";
                if (!memoryCache.TryGetValue(memKey, out string file))
                {
                    string search = await HttpClient.Get($"{AppInit.conf.Kinoprofi.host}/search/f:{HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, useproxy: AppInit.conf.Kinoprofi.useproxy);
                    if (search == null)
                        return Content(string.Empty);

                    string keyid = null;
                    foreach (string row in Regex.Replace(search, "[\n\r\t]+", "").Split("sh-block ns").Skip(1))
                    {
                        if (Regex.Match(row, "itemprop=\"name\" content=\"([^\"]+)\"").Groups[1].Value.ToLower() != title.ToLower())
                            continue;

                        if (Regex.Match(row, "<b>Год</b> ?<i>([0-9]{4})</i>").Groups[1].Value == year.ToString())
                        {
                            keyid = Regex.Match(row, "href=\"https?://[^/]+/([0-9]+)-[^\"]+\" itemprop=\"url\"").Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(keyid))
                                break;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(keyid))
                        return Content(string.Empty);

                    string session_id = Regex.Match(search, "var session_id += '([^']+)'").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(session_id))
                        return Content(string.Empty);

                    string json = await HttpClient.Post($"{AppInit.conf.Kinoprofi.apihost}/getplay", $"key%5Bid%5D={keyid}&pl_type=movie&session={session_id}&is_mobile=0&dle_group=5", timeoutSeconds: 8, useproxy: AppInit.conf.Kinoprofi.useproxy);
                    if (json == null || !json.Contains(".m3u8"))
                        return Content(string.Empty);

                    file = Regex.Match(json, "\"hls\":\"(https?:[^\"]+)\"").Groups[1].Value.Replace("\\", "");
                    if (string.IsNullOrWhiteSpace(file))
                        return Content(string.Empty);

                    memoryCache.Set(memKey, file, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
                }

                file = HostStreamProxy(true, file);
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
