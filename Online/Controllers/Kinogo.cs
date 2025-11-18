using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;
using System.Net;

namespace Online.Controllers
{
    public class Kinogo : BaseOnlineController
    {
        public class SearchModel
        {
            public string link { get; set; }

            public SimilarTpl? similar { get; set; }
        }

        [HttpGet]
        [Route("lite/kinogo")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, bool rjson, string href, bool similar, int s = -1, int t = -1)
        {
            var init = await loadKit(AppInit.conf.Kinogo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (rch.IsNotSupport("web,cors", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            #region search
            if (string.IsNullOrEmpty(href))
            {
                if (string.IsNullOrEmpty(title) || year == 0)
                    return OnError("search params");

                reset_search:
                var search = await InvokeCache<SearchModel>($"kinogo:search:{title}:{year}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";

                    string searchHtml = rch.enable 
                        ? await rch.Post($"{init.corsHost()}/index.php?do=search", data, httpHeaders(init)) 
                        : await Http.Post($"{init.corsHost()}/index.php?do=search", data, timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init));

                    if (searchHtml == null)
                        return res.Fail("search");

                    var result = SearchResult(searchHtml, title, year);
                    if (result == null)
                        return res.Fail("search-result");

                    return result;
                });

                if (similar || string.IsNullOrEmpty(search.Value?.link))
                    return OnResult(search, () => rjson ? search.Value.similar.Value.ToJson() : search.Value.similar.Value.ToHtml());

                if (string.IsNullOrEmpty(search.Value?.link))
                {
                    if (IsRhubFallback(search, init))
                        goto reset_search;

                    return OnError();
                }

                href = search.Value?.link;
            }
            #endregion

            if (string.IsNullOrEmpty(href))
                return OnError();

            #region embed
            reset_embed:
            var cache = await InvokeCache<JArray>(rch.ipkey(href, proxyManager), cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                string targetHref = $"{init.corsHost()}/{href}";

                string html = rch.enable 
                    ? await rch.Get(targetHref, httpHeaders(init)) 
                    : await Http.Get(init.cors(targetHref), timeoutSeconds: 8, proxy: proxy.proxy, headers: httpHeaders(init));

                if (html == null) 
                    return res.Fail("html");

                string iframe = Regex.Match(html, "<iframe [^>]+data-src=\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(iframe))
                    return res.Fail("iframe");

                string embedUrl = iframe.StartsWith("//") ? $"https:{iframe}" : iframe;
                var embedHeaders = httpHeaders(init, HeadersModel.Init("referer", targetHref));

                string embedHtml = rch.enable 
                    ? await rch.Get(init.cors(embedUrl), embedHeaders) 
                    : await PlaywrightBrowser.Get(init, init.cors(embedUrl), headers: embedHeaders, proxy.data);

                if (embedHtml == null)
                    return res.Fail("embed");

                string playlistPath = Regex.Match(embedHtml, "\"file\":\"(/playlist/[^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(playlistPath))
                    return res.Fail("playlist");

                string playlistUrl = Regex.Match(embedUrl, "^(https?://[^/]+)").Groups[1].Value + playlistPath;
                var playlistHeaders = httpHeaders(init, HeadersModel.Init("referer", embedUrl));

                string playlistJson = rch.enable 
                    ? await rch.Get(init.cors(playlistUrl), playlistHeaders) 
                    : await PlaywrightBrowser.Get(init, init.cors(playlistUrl), headers: playlistHeaders, proxy.data);

                if (playlistJson == null)
                    return res.Fail("playlist-json");

                try
                {
                    var playlist = JArray.Parse(playlistJson);
                    if (playlist == null || playlist.Count == 0)
                        return res.Fail("playlist");

                    return playlist;
                }
                catch
                {
                    return res.Fail("DeserializeObject");
                }
            });

            if (IsRhubFallback(cache, init))
                goto reset_embed;
            #endregion

            return OnResult(cache, () =>
            {
                return BuildResult(init, cache.Value, title, original_title, year, s, t, rjson, href, proxy.proxy);

            }, gbcache: !rch.enable);
        }


        #region BuildResult
        string BuildResult(OnlinesSettings init, JArray playlist, string title, string original_title, int year, int s, int t, bool rjson, string href, WebProxy proxy)
        {
            if (playlist.First.Value<string>("file") != null)
            {
                var mtpl = new MovieTpl(title, original_title);

                foreach (var source in playlist)
                {
                    string voice = source.Value<string>("title");
                    string file = source.Value<string>("file");

                    if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(file))
                        continue;

                    if (file.StartsWith("//"))
                        file = "https:" + file;

                    #region subtitle
                    var subtitles = new SubtitleTpl();
                    string _subs = source.Value<string>("subtitle");

                    if (!string.IsNullOrEmpty(_subs))
                    {
                        var match = new Regex("\\[([^\\]]+)\\]([^\\[\\,]+)").Match(_subs);
                        while (match.Success)
                        {
                            string srt = match.Groups[2].Value;
                            if (srt.StartsWith("//"))
                                srt = "https:" + srt;

                            subtitles.Append(match.Groups[1].Value, HostStreamProxy(init, srt, proxy: proxy));
                            match = match.NextMatch();
                        }
                    }
                    #endregion

                    voice = Regex.Replace(voice, "<[^>]+>", "");
                    mtpl.Append(voice, HostStreamProxy(init, file, proxy: proxy), subtitles: subtitles, vast: init.vast);
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();
            }
            else
            {
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);
                string enc_href = HttpUtility.UrlEncode(href);

                if (s == -1)
                {
                    var tpl = new SeasonTpl(playlist.Count);
                    foreach (var season in playlist)
                    {
                        string _s = Regex.Match(season.Value<string>("title"), " ([0-9]+)$").Groups[1].Value;
                        if (!string.IsNullOrEmpty(_s))
                        {
                            string link = $"{host}/lite/kinogo?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={_s}";
                            tpl.Append($"{_s} сезон", link, _s);
                        }
                    }

                    return rjson ? tpl.ToJson() : tpl.ToHtml();
                }
                else
                {
                    var episodes = playlist.First(i => i.Value<string>("title").EndsWith($" {s}"))["folder"];

                    #region Перевод
                    var vtpl = new VoiceTpl();
                    var hashSet = new HashSet<int>();

                    foreach (var episode in episodes)
                    {
                        foreach (var voice in episode["folder"])
                        {
                            int voice_id = voice.Value<int>("voice_id");
                            if (!hashSet.Add(voice_id))
                                continue;

                            if (t == -1)
                                t = voice_id;

                            string link = $"{host}/lite/kinogo?rjson={rjson}&title={enc_title}&original_title={enc_original_title}&year={year}&href={enc_href}&s={s}&t={voice_id}";
                            bool active = t == voice_id;

                            vtpl.Append(voice.Value<string>("title"), active, link);
                        }
                    }
                    #endregion

                    var etpl = new EpisodeTpl(episodes.Count());
                    string sArhc = s.ToString();

                    foreach (var episode in episodes)
                    {
                        string name = episode.Value<string>("title");
                        string file = episode["folder"].FirstOrDefault(i => i.Value<int>("voice_id") == t)?.Value<string>("file");

                        if (string.IsNullOrEmpty(file))
                            continue;

                        if (file.StartsWith("//"))
                            file = "https:" + file;

                        #region subtitle
                        var subtitles = new SubtitleTpl();
                        string _subs = episode.Value<string>("subtitle");

                        if (!string.IsNullOrEmpty(_subs))
                        {
                            var match = new Regex("\\[([^\\]]+)\\]([^\\[\\,]+)").Match(_subs);
                            while (match.Success)
                            {
                                string srt = match.Groups[2].Value;
                                if (srt.StartsWith("//"))
                                    srt = "https:" + srt;

                                subtitles.Append(match.Groups[1].Value, HostStreamProxy(init, srt, proxy: proxy));
                                match = match.NextMatch();
                            }
                        }
                        #endregion

                        string stream = HostStreamProxy(init, file, proxy: proxy);
                        etpl.Append(name, title ?? original_title, sArhc, Regex.Match(name, " ([0-9]+)$").Groups[1].Value, stream, subtitles: subtitles, vast: init.vast);
                    }

                    if (rjson)
                        return etpl.ToJson(vtpl);

                    return vtpl.ToHtml() + etpl.ToHtml();
                }
            }
        }
        #endregion

        #region SearchResult
        SearchModel SearchResult(string html, string title, int year)
        {
            string stitle = StringConvert.SearchName(title);

            var matches = Regex.Matches(html, "<div id=\"[0-9]+\" class=\"shortstory\">(.*?)<div class=\"shortstory__meta\">", RegexOptions.Singleline);
            if (matches.Count == 0)
                return null;

            var similar = new SimilarTpl(matches.Count);

            string link = null;

            foreach (Match match in matches)
            {
                string block = match.Groups[1].Value;
                string href = Regex.Match(block, "<a href=\"https?://[^/]+/([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(href))
                    continue;

                string name = Regex.Match(block, "<h2>([^<]+)</h2>").Groups[1].Value;
                if (string.IsNullOrEmpty(name))
                    continue;
                
                string blockYear = Regex.Match(block, "Год выпуска:</b><a[^>]*>([0-9]{4})").Groups[1].Value;

                string img = Regex.Match(block, "<img\\s+data-src=\"([^\"]+)\"").Groups[1].Value;
                if (!string.IsNullOrEmpty(img))
                    img = AppInit.conf.Kinogo.corsHost() + img;

                string uri = host + $"lite/kinogo?href={HttpUtility.UrlEncode(href)}";
                similar.Append(name, blockYear, string.Empty, uri, PosterApi.Size(img));

                if (StringConvert.SearchName(name).Contains(stitle) && blockYear == year.ToString())
                    link = href;
            }

            if (string.IsNullOrEmpty(link) && similar.data.Count == 0)
                return null;

            return new SearchModel() 
            {
                link = link,
                similar = similar
            };
        }
        #endregion
    }
}