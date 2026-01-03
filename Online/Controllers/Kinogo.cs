using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;

namespace Online.Controllers
{
    public class Kinogo : BaseOnlineController
    {
        public Kinogo() : base(AppInit.conf.Kinogo) { }

        public class SearchModel
        {
            public string link { get; set; }

            public SimilarTpl? similar { get; set; }
        }

        [HttpGet]
        [Route("lite/kinogo")]
        async public Task<ActionResult> Index(string title, string original_title, int year, bool rjson, string href, bool similar, int s = -1, int t = -1)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            #region search
            if (string.IsNullOrEmpty(href))
            {
                if (string.IsNullOrEmpty(title))
                    return OnError("search params");

                reset_search:
                var search = await InvokeCacheResult<SearchModel>($"kinogo:search:{title}:{year}", 20, async e =>
                {
                    string data = $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}";

                    string searchHtml = await httpHydra.Post($"{init.corsHost()}/index.php?do=search", data);

                    if (searchHtml == null)
                        return e.Fail("search");

                    var result = SearchResult(searchHtml, title, year);
                    if (result == null)
                        return e.Fail("search-result");

                    return e.Success(result);
                });

                if (similar || string.IsNullOrEmpty(search.Value?.link))
                    return await ContentTpl(search, () => search.Value.similar);

                if (string.IsNullOrEmpty(search.Value?.link))
                {
                    if (IsRhubFallback(search))
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
            var cache = await InvokeCacheResult<JArray>(ipkey(href), 20, async e =>
            {
                string targetHref = $"{init.corsHost()}/{href}";

                string html = await httpHydra.Get(targetHref);

                if (html == null) 
                    return e.Fail("html");

                string iframe = Regex.Match(html, "<iframe [^>]+data-src=\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(iframe))
                    return e.Fail("iframe");

                string embedUrl = iframe.StartsWith("//") ? $"https:{iframe}" : iframe;
                var embedHeaders = httpHeaders(init, HeadersModel.Init("referer", targetHref));

                string embedHtml = rch?.enable == true
                    ? await rch.Get(init.cors(embedUrl), embedHeaders) 
                    : await PlaywrightBrowser.Get(init, init.cors(embedUrl), headers: embedHeaders, proxy_data);

                if (embedHtml == null)
                    return e.Fail("embed");

                string fileEncode = Regex.Match(embedHtml, "\"file\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(fileEncode))
                    return e.Fail("fileEncode");

                string playlistJson = await DecodeFile(init, fileEncode);
                if (string.IsNullOrEmpty(playlistJson))
                    return e.Fail("playlistJson");

                try
                {
                    var playlist = JArray.Parse(playlistJson);
                    if (playlist == null || playlist.Count == 0)
                        return e.Fail("playlist");

                    return e.Success(playlist);
                }
                catch
                {
                    return e.Fail("DeserializeObject");
                }
            });

            if (IsRhubFallback(cache))
                goto reset_embed;
            #endregion

            return await ContentTpl(cache, () => BuildResult(cache.Value, title, original_title, year, s, t, rjson, href));
        }


        #region BuildResult
        ITplResult BuildResult(JArray playlist, string title, string original_title, int year, int s, int t, bool rjson, string href)
        {
            if (playlist.First.Value<string>("file") != null)
            {
                var mtpl = new MovieTpl(title, original_title);

                foreach (var source in playlist)
                {
                    string voice = source.Value<string>("title");
                    string file = source.Value<string>("file");

                    if (string.IsNullOrEmpty(voice) || string.IsNullOrEmpty(file))
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

                            subtitles.Append(match.Groups[1].Value, HostStreamProxy(srt));
                            match = match.NextMatch();
                        }
                    }
                    #endregion

                    voice = Regex.Replace(voice, "<[^>]+>", "");
                    mtpl.Append(voice, HostStreamProxy(file), subtitles: subtitles, vast: init.vast);
                }

                return mtpl;
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

                    return tpl;
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

                    var etpl = new EpisodeTpl(vtpl, episodes.Count());

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

                                subtitles.Append(match.Groups[1].Value, HostStreamProxy(srt));
                                match = match.NextMatch();
                            }
                        }
                        #endregion

                        string stream = HostStreamProxy(file);
                        etpl.Append(name, title ?? original_title, s.ToString(), Regex.Match(name, " ([0-9]+)$").Groups[1].Value, stream, subtitles: subtitles, vast: init.vast);
                    }

                    return etpl;
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

                string uri = $"{host}/lite/kinogo?href={HttpUtility.UrlEncode(href)}";
                similar.Append(name, blockYear, string.Empty, uri, PosterApi.Size(img));

                if (StringConvert.SearchName(name).Contains(stitle) && blockYear == year.ToString())
                    link = href;
            }

            if (string.IsNullOrEmpty(link) && similar.IsEmpty)
                return null;

            return new SearchModel() 
            {
                link = link,
                similar = similar
            };
        }
        #endregion

        #region DecodeFile
        async Task<string> DecodeFile(OnlinesSettings init, string fileEncode)
        {
            try
            {
                using (var browser = new PlaywrightBrowser())
                {
                    var page = await browser.NewPageAsync(init.plugin).ConfigureAwait(false);
                    if (page == null)
                        return null;

                    var html = $@"<!doctype html>
                        <html lang=""ru"">
                          <body>
                            <script>
                              {FileCache.ReadAllText("data/cinemar_playerjs.js")}
                              Cinemar({{""file"":""{fileEncode}""}})
                            </script>
                          </body>
                        </html>";

                    await page.SetContentAsync(html).ConfigureAwait(false);

                    return await page.EvaluateAsync<string>("() => JSON.stringify(window.playlist)");
                }
            }
            catch { return null; }
        }
        #endregion
    }
}