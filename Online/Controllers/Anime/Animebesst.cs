using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
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
        async public Task<ActionResult> Index(string rchtype, string title, string uri, int s, string account_email, bool rjson = false)
        {
            var init = AppInit.conf.Animebesst;

            if (!init.enable || string.IsNullOrWhiteSpace(title))
                return OnError();

            if (init.rhub && !AppInit.conf.rch.enable)
                return ShowError(RchClient.ErrorMsg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            var rch = new RchClient(HttpContext, host, init.rhub);

            if (string.IsNullOrWhiteSpace(uri))
            {
                #region Поиск
                string memkey = $"animebesst:search:{title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, string uri, string s)> catalog))
                {
                    if (rchtype != "apk")
                        return ShowError(RchClient.ErrorType(rchtype));

                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";
                    string search = init.rhub ? await rch.Post($"{init.corsHost()}/index.php?do=search", data) : await HttpClient.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null)
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    catalog = new List<(string title, string year, string uri, string s)>();

                    foreach (string row in search.Split("id=\"sidebar\"")[0].Split("class=\"shortstory-listab\"").Skip(1))
                    {
                        if (row.Contains("Новости"))
                            continue;

                        var g = Regex.Match(row, "class=\"shortstory-listab-title\"><a href=\"(https?://[^\"]+\\.html)\">([^<]+)</a>").Groups;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                        {
                            string season = "0";
                            if (g[2].Value.Contains("сезон"))
                            {
                                season = Regex.Match(g[2].Value, "([0-9]+) сезон").Groups[1].Value;
                                if (string.IsNullOrEmpty(season))
                                    season = "1";
                            }

                            //if (g[2].Value.ToLower().Contains(title.ToLower()))
                                catalog.Add((g[2].Value, Regex.Match(row, "\">([0-9]{4})</a>").Groups[1].Value, g[1].Value, season));
                        }
                    }

                    if (catalog.Count == 0 && !search.Contains(">Поиск по сайту<"))
                        return OnError();

                    if (!init.rhub)
                        proxyManager.Success();

                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 0)
                    return OnError();

                if (catalog.Count == 1)
                    return LocalRedirect($"/lite/animebesst?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(catalog[0].uri)}&s={catalog[0].s}&account_email={HttpUtility.UrlEncode(account_email)}");

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/animebesst?title={HttpUtility.UrlEncode(title)}&uri={HttpUtility.UrlEncode(res.uri)}&s={res.s}");

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"animebesst:playlist:{uri}";
                if (!hybridCache.TryGetValue(memKey, out List<(string episode, string name, string uri)> links))
                {
                    if (rchtype != "apk")
                        return ShowError(RchClient.ErrorType(rchtype));

                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string news = init.rhub ? await rch.Get(uri) : await HttpClient.Get(uri, timeoutSeconds: 10, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (news == null)
                        return OnError(proxyManager, refresh_proxy: !init.rhub);

                    string videoList = Regex.Match(news, "var videoList ?=([^\n\r]+)").Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(videoList))
                        return OnError();

                    links = new List<(string episode, string name, string uri)>();
                    var match = Regex.Match(videoList, "\"id\":\"([0-9]+)( [^\"]+)?\",\"link\":\"(https?:)?\\\\/\\\\/([^\"]+)\"");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[4].Value))
                            links.Add((match.Groups[1].Value, match.Groups[2].Value.Trim(), match.Groups[4].Value.Replace("\\", "")));

                        match = match.NextMatch();
                    }

                    if (links.Count == 0)
                        return OnError();

                    if (!init.rhub)
                        proxyManager.Success();

                    hybridCache.Set(memKey, links, cacheTime(30, init: init));
                }

                var etpl = new EpisodeTpl();

                foreach (var l in links)
                {
                    string name = string.IsNullOrEmpty(l.name) ? $"{l.episode} серия" : $"{l.episode} {l.name}";
                    string voice_name = !string.IsNullOrEmpty(l.name) ? Regex.Replace(l.name, "(^\\(|\\)$)", "") : "";

                    string link = $"{host}/lite/animebesst/video.m3u8?uri={HttpUtility.UrlEncode(l.uri)}&account_email={HttpUtility.UrlEncode(account_email)}";

                    etpl.Append(name, $"{title} / {name}", s.ToString(), l.episode, link, voice_name: Regex.Unescape(voice_name));
                }

                return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/animebesst/video.m3u8")]
        async public Task<ActionResult> Video(string uri)
        {
            var init = AppInit.conf.Animebesst;

            if (!init.enable)
                return OnError();

            string memKey = $"animebesst:video:{uri}";
            if (!hybridCache.TryGetValue(memKey, out string hls))
            {
                var rch = new RchClient(HttpContext, host, init.rhub);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string iframe;
                if (init.rhub)
                {
                    iframe = await rch.Get(init.cors($"https://{uri}"), headers: new Dictionary<string, string>() { ["referer"] = init.host });
                }
                else
                {
                    iframe = await HttpClient.Get(init.cors($"https://{uri}"), referer: init.host, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init), httpversion: 2);
                }

                if (iframe == null)
                    return OnError(proxyManager, refresh_proxy: !init.rhub);

                hls = Regex.Match(iframe, "file:\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(hls))
                    return OnError();

                if (!init.rhub)
                    proxyManager.Success();

                hybridCache.Set(memKey, hls, cacheTime(30, init: init));
            }

            return Redirect(HostStreamProxy(init, hls, proxy: proxyManager.Get(), plugin: "animebesst"));
        }
        #endregion
    }
}
