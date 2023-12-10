using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Text.RegularExpressions;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class Animebesst : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("animebesst", AppInit.conf.Animebesst);

        [HttpGet]
        [Route("lite/animebesst")]
        async public Task<ActionResult> Index(string title, string uri, int s, string account_email)
        {
            if (!AppInit.conf.Animebesst.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                string memkey = $"animebesst:search:{title}";
                if (!memoryCache.TryGetValue(memkey, out List<(string title, string year, string uri, string s)> catalog))
                {
                    string search = await HttpClient.Post($"{AppInit.conf.Animebesst.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxyManager.Get());
                    if (search == null)
                        return OnError(proxyManager);

                    catalog = new List<(string title, string year, string uri, string s)>();

                    foreach (string row in search.Split("id=\"sidebar\"")[0].Split("class=\"shortstory-listab\"").Skip(1))
                    {
                        var g = Regex.Match(row, "class=\"shortstory-listab-title\"><a href=\"(https?://[^\"]+\\.html)\">([^<]+)</a>").Groups;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                        {
                            string season = "0";
                            if (!g[2].Value.Contains("сезон") || g[2].Value.Contains("1 сезон"))
                                season = "1";

                            if (g[2].Value.ToLower().Contains(title.ToLower()))
                                catalog.Add((g[2].Value, Regex.Match(row, "\">([0-9]{4})</a>").Groups[1].Value, g[1].Value, season));
                        }
                    }

                    if (catalog.Count == 0)
                        return OnError(proxyManager);

                    memoryCache.Set(memkey, catalog, cacheTime(40));
                }

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animebesst?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog[0].uri)}&s={catalog[0].s}&account_email={HttpUtility.UrlEncode(account_email)}");

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                {
                    string link = $"{host}/lite/animebesst?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}";

                    stpl.Append(res.title, res.year, string.Empty, link);
                }

                return Content(stpl.ToHtml(), "text/html; charset=utf-8");
                #endregion
            }
            else 
            {
                #region Серии
                bool firstjson = true;
                string html = "<div class=\"videos__line\">";

                string memKey = $"animebesst:playlist:{uri}";
                if (!memoryCache.TryGetValue(memKey, out List<(string episode, string name, string uri)> links))
                {
                    string news = await HttpClient.Get(uri, timeoutSeconds: 10, proxy: proxyManager.Get());
                    string videoList = Regex.Match(news ?? "", "var videoList = ([^\n\r]+)").Groups[1].Value;

                    if (string.IsNullOrWhiteSpace(videoList))
                        return OnError(proxyManager);

                    links = new List<(string episode, string name, string uri)>();
                    var match = Regex.Match(videoList, "\"id\":\"([0-9]+)( [^\"]+)?\",\"link\":\"(https?:)?\\\\/\\\\/([^\"]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[4].Value))
                            links.Add((match.Groups[1].Value, match.Groups[2].Value.Trim(), match.Groups[4].Value.Replace("\\", "")));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return OnError(proxyManager);

                    memoryCache.Set(memKey, links, cacheTime(30));
                }

                foreach (var l in links)
                {
                    string name = string.IsNullOrEmpty(l.name) ? $"{l.episode} серия" : $"{l.episode} {l.name}";
                    string voice_name = !string.IsNullOrEmpty(l.name) ? Regex.Replace(l.name, "(^\\(|\\)$)", "") : "";

                    string link = $"{host}/lite/animebesst/video.m3u8?uri={HttpUtility.UrlEncode(l.uri)}&account_email={HttpUtility.UrlEncode(account_email)}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + l.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + $"{title} / {name}" + "\",\"voice_name\":\"" + voice_name + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + name + "</div></div>";
                    firstjson = true;
                }

                return Content(html + "</div>", "text/html; charset=utf-8");
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animebesst/video.m3u8")]
        async public Task<ActionResult> Video(string uri)
        {
            if (!AppInit.conf.Animebesst.enable)
                return OnError();

            string memKey = $"animebesst:video:{uri}";
            if (!memoryCache.TryGetValue(memKey, out string hls))
            {
                string iframe = await HttpClient.Get($"https://{uri}", timeoutSeconds: 8, proxy: proxyManager.Get());
                hls = Regex.Match(iframe ?? "", "file:\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;

                if (string.IsNullOrEmpty(hls))
                    return OnError(proxyManager);

                memoryCache.Set(memKey, hls, cacheTime(30));
            }

            return Redirect(HostStreamProxy(AppInit.conf.Animebesst, hls, proxy: proxyManager.Get(), plugin: "animebesst"));
        }
        #endregion
    }
}
