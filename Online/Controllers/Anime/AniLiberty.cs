using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Templates;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers.LITE
{
    public class AniLiberty : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/aniliberty")]
        async public ValueTask<ActionResult> Index(string title, int year, int releases, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.AniLiberty);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(AppInit.conf.AniLiberty);
            var proxy = proxyManager.Get();
            
            var rch = new RchClient(HttpContext, host, init, requestInfo);

            if (releases == 0)
            {
                #region Поиск
                string stitle = StringConvert.SearchName(title);
                if (string.IsNullOrEmpty(stitle))
                    return OnError();

                string memkey = $"aniliberty:search:{title}:{similar}";
                if (!hybridCache.TryGetValue(memkey, out List<(string title, string year, int releases, string cover)> catalog))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string req_uri = $"{init.corsHost()}/api/v1/app/search/releases?query={HttpUtility.UrlEncode(title)}";
                    var search = rch.enable ? await rch.Get<JArray>(req_uri, httpHeaders(init)) :
                                              await HttpClient.Get<JArray>(req_uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));

                    if (search == null || search.Count == 0)
                        return OnError(proxyManager, refresh_proxy: !rch.enable);

                    bool checkName = true;
                    catalog = new List<(string title, string year, int releases, string cover)>(search.Count);

                    retry: foreach (var anime in search)
                    {
                        var name = anime["name"];
                        string name_main = StringConvert.SearchName(name.Value<string>("main"));
                        string name_english = StringConvert.SearchName(name.Value<string>("english"));

                        if (!checkName || similar || (name_main != null && name_main.StartsWith(stitle)) || (name_english != null && name_english.StartsWith(stitle)))
                        {
                            int id = anime.Value<int>("id");
                            int releaseDate = anime.Value<int>("year");

                            string img = null;
                            var cover = anime["poster"];
                            if (cover != null)
                                img = init.host + cover.Value<string>("src");

                            catalog.Add(($"{name.Value<string>("main")} / {name.Value<string>("english")}", releaseDate.ToString(), id, img));
                        }
                    }

                    if (catalog.Count == 0)
                    {
                        if (checkName && similar == false)
                        {
                            checkName = false;
                            goto retry;
                        }

                        return OnError();
                    }

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memkey, catalog, cacheTime(40, init: init));
                }

                if (!similar && catalog.Count == 1)
                    return LocalRedirect(accsArgs($"/lite/aniliberty?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&releases={catalog.First().releases}"));

                var stpl = new SimilarTpl(catalog.Count);

                foreach (var res in catalog)
                    stpl.Append(res.title, res.year, string.Empty, $"{host}/lite/aniliberty?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&releases={res.releases}", PosterApi.Size(res.cover));

                return ContentTo(rjson ? stpl.ToJson() : stpl.ToHtml());
                #endregion
            }
            else 
            {
                #region Серии
                string memKey = $"aniliberty:releases:{releases}";
                if (!hybridCache.TryGetValue(memKey, out JObject root))
                {
                    if (rch.IsNotConnected())
                        return ContentTo(rch.connectionMsg);

                    string req_uri = $"{init.corsHost()}/api/v1/anime/releases/{releases}";

                    root = rch.enable ? await rch.Get<JObject>(req_uri, httpHeaders(init)) : 
                                        await HttpClient.Get<JObject>(req_uri, timeoutSeconds: 8, httpversion: 2, proxy: proxy, headers: httpHeaders(init));

                    if (root == null || !root.ContainsKey("episodes"))
                        return OnError(proxyManager, refresh_proxy: !rch.enable);

                    if (!rch.enable)
                        proxyManager.Success();

                    hybridCache.Set(memKey, root, cacheTime(20, init: init));
                }

                var episodes = root["episodes"] as JArray;
                var etpl = new EpisodeTpl(episodes.Count);

                foreach (var episode in episodes)
                {
                    string alias = root.Value<string>("alias") ?? "";
                    string season = Regex.Match(alias, "-([0-9]+)(nd|th)").Groups[1].Value;
                    if (string.IsNullOrEmpty(season))
                    {
                        season = Regex.Match(alias, "season-([0-9]+)").Groups[1].Value;
                        if (string.IsNullOrEmpty(season))
                            season = "1";
                    }

                    string number = episode.Value<string>("ordinal");

                    string name = episode.Value<string>("name");
                    name = string.IsNullOrEmpty(name) ? $"{number} серия" : name;

                    var streams = new StreamQualityTpl();
                    foreach (var f in new List<(string quality, string url)> 
                    { 
                        ("1080p", episode.Value<string>("hls_1080")), 
                        ("720p", episode.Value<string>("hls_720")), 
                        ("480p", episode.Value<string>("hls_480")) 
                    })
                    {
                        if (string.IsNullOrEmpty(f.url))
                            continue;

                        streams.Append(HostStreamProxy(init, f.url, proxy: proxy), f.quality);
                    }

                    etpl.Append(name, title, season, number, streams.Firts().link, streamquality: streams);
                }
                
                return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                #endregion
            }
        }
    }
}
