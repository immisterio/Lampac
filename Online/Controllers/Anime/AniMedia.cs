using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using System.Web;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;

namespace Lampac.Controllers.LITE
{
    public class AniMedia : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.AniMedia);

        [HttpGet]
        [Route("lite/animedia")]
        async public Task<ActionResult> Index(string title, string code, int entry_id, int s = -1, bool rjson = false)
        {
            var init = loadKit(AppInit.conf.AniMedia.Clone());
            if (IsBadInitialization(init, out ActionResult action, rch: false))
                return action;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            if (string.IsNullOrWhiteSpace(code))
            {
                #region Поиск
                string memkey = $"animedia:search:{title}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string code)> catalog))
                {
                    string search = await HttpClient.Get($"{init.corsHost()}/ajax/search_result_search_page_2/P0?limit=12&keywords={HttpUtility.UrlEncode(title)}&orderby_sort=entry_date|desc", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                    if (search == null)
                        return OnError(proxyManager);

                    catalog = new List<(string title, string url)>();

                    foreach (string row in search.Split("<div class=\"ads-list__item\">").Skip(1))
                    {
                        var g = Regex.Match(row, "href=\"/anime/([^\"]+)\"[^>]+ class=\"h3 ads-list__item__title\">([^<]+)</a>").Groups;

                        if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                            catalog.Add((g[2].Value, g[1].Value));
                    }

                    if (catalog.Count == 0 && !search.Contains("xads-list"))
                        return OnError();

                    proxyManager.Success();
                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (catalog.Count == 0)
                    return OnError();

                if (catalog.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/animedia?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&code={catalog[0].code}"));

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, string.Empty, string.Empty, $"{host}/lite/animedia?title={HttpUtility.UrlEncode(title)}&code={res.code}");

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                if (s == -1)
                {
                    #region Сезоны
                    string memKey = $"animedia:seasons:{code}";
                    if (!hybridCache.TryGetValue(memKey, out List<(string name, string uri, string season)> links))
                    {
                        string news = await HttpClient.Get($"{init.corsHost()}/anime/{code}/1/1", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                        if (news == null)
                            return OnError(proxyManager);

                        string entryid = Regex.Match(news, "name=\"entry_id\" value=\"([0-9]+)\"").Groups[1].Value;
                        if (string.IsNullOrEmpty(entryid))
                            return OnError();

                        links = new List<(string, string, string)>();

                        var match = Regex.Match(news, $"<a href=\"/anime/{code}/([0-9]+)/1\" class=\"item\">([^<]+)</a>");
                        while (match.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                                links.Add((match.Groups[2].Value.ToLower(), $"lite/animedia?title={HttpUtility.UrlEncode(title)}&code={code}&s={match.Groups[1].Value}&entry_id={entryid}", match.Groups[1].Value));

                            match = match.NextMatch();
                        }

                        if (links.Count == 0)
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memKey, links, cacheTime(30, init: init));
                    }

                    var tpl = new SeasonTpl(links.Count);

                    foreach (var l in links)
                        tpl.Append(l.name, $"{host}/{l.uri}", l.season);

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Серии
                    var etpl = new EpisodeTpl();
                    var proxy = proxyManager.Get();

                    string memKey = $"animedia:playlist:{entry_id}:{s}";
                    if (!hybridCache.TryGetValue(memKey, out List<(string name, string uri)> links))
                    {
                        var playlist = await HttpClient.Get<JArray>($"{init.corsHost()}/embeds/playlist-j.txt/{entry_id}/{s}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                        if (playlist == null || playlist.Count == 0)
                            return OnError(proxyManager);

                        links = new List<(string name, string uri)>();

                        foreach (var pl in playlist)
                        {
                            string name = pl.Value<string>("title");
                            string file = pl.Value<string>("file");
                            if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(file))
                                links.Add((name, file));
                        }

                        if (links.Count == 0)
                            return OnError();

                        proxyManager.Success();
                        hybridCache.Set(memKey, links, cacheTime(30, init: init));
                    }

                    foreach (var l in links)
                    {
                        string link = HostStreamProxy(init, l.uri, proxy: proxy);
                        etpl.Append(l.name, $"{title} / {l.name.ToLower()}", s.ToString(), Regex.Match(l.name, "([0-9]+)$").Groups[1].Value, link, vast: init.vast);
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                    #endregion
                }
            }
        }
    }
}
