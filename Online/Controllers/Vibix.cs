using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using Shared.Models.Online.Vibix;

namespace Online.Controllers
{
    public class Vibix : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/vibix")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title,  int s = -1, bool rjson = false, bool origsource = false)
        {
            var init = await loadKit(AppInit.conf.Vibix);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var data = await search(proxyManager, init, imdb_id, kinopoisk_id);
            if (data == null)
                return OnError();

            var rch = new RchClient(HttpContext, host, init, requestInfo);

            if (rch.IsNotConnected() || rch.IsRequiredConnected())
                return ContentTo(rch.connectionMsg);

            if (rch.IsNotSupport(out string rch_error))
                return ShowError(rch_error);

            reset:
            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vibix:iframe:{data.iframe_url}:{init.token}", proxyManager), cacheTime(20, rhub: 2, init: init), rch.enable ? null : proxyManager, async res =>
            {
                string api_url = data.iframe_url
                    .Replace("/embed/", "/api/v1/embed/")
                    .Replace("/embed-serials/", "/api/v1/embed-serials/");

                api_url += $"?iframe_url={HttpUtility.UrlEncode(data.iframe_url)}";
                api_url += $"&kp={CrypTo.unic(6).ToLower()}";

                var api_headers = httpHeaders(init, HeadersModel.Init(
                    ("accept", "*/*"),
                    ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("referer", data.iframe_url)
                ));

                var root = rch.enable 
                    ? await rch.Get<JObject>(init.cors(api_url), api_headers) 
                    : await Http.Get<JObject>(init.cors(api_url), timeoutSeconds: 8, proxy: proxy, headers: api_headers, httpversion: 2);

                if (root == null || !root.ContainsKey("data") || root["data"]?["playlist"] == null)
                    return res.Fail("root");

                return new EmbedModel() { playlist = root["data"]["playlist"].ToObject<Seasons[]>() };
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            if (data.type == "movie")
            {
                #region Фильм
                return OnResult(cache, () => 
                {
                    var mtpl = new MovieTpl(title, original_title, 1);

                    foreach (var movie in cache.Value.playlist)
                    {
                        var streams = new StreamQualityTpl();

                        foreach (string q in new string[] { "1080", "720", "480" })
                        {
                            var g = new Regex($"{q}p?\\](\\{{[^\\}}]+\\}})?(?<file>https?://[^,\t\\[\\;\\{{ ]+)").Match(movie.file).Groups;

                            if (!string.IsNullOrEmpty(g["file"].Value))
                                streams.Append(HostStreamProxy(init, g["file"].Value, proxy: proxy), $"{q}p");
                        }

                        mtpl.Append(movie.title, streams.Firts().link, streamquality: streams, vast: init.vast);
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, origsource: origsource, gbcache: !rch.enable);
                #endregion
            }
            else
            {
                #region Сериал
                return OnResult(cache, () =>
                {
                    string enc_title = HttpUtility.UrlEncode(title);
                    string enc_original_title = HttpUtility.UrlEncode(original_title);

                    if (s == -1)
                    {
                        var tpl = new SeasonTpl(cache.Value.playlist.Length);

                        foreach (var season in cache.Value.playlist)
                        {
                            string name = season.title;
                            if (int.TryParse(Regex.Match(name, "([0-9]+)$").Groups[1].Value, out int _s) && _s > 0)
                            {
                                string link = $"{host}/lite/vibix?rjson={rjson}&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}&title={enc_title}&original_title={enc_original_title}&s={_s}";
                                tpl.Append($"{_s} сезон", link, _s);
                            }
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                    }
                    else
                    {
                        var etpl = new EpisodeTpl();
                        string sArhc = s.ToString();

                        foreach (var season in cache.Value.playlist)
                        {
                            if (!season.title.EndsWith($" {s}"))
                                continue;

                            foreach (var episode in season.folder)
                            {
                                string name = episode.title;
                                string file = episode.folder?.First().file ?? episode.file;

                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(file))
                                    continue;

                                var streams = new StreamQualityTpl();

                                foreach (string q in new string[] { "1080", "720", "480" })
                                {
                                    var g = new Regex($"{q}p?\\](\\{{[^\\}}]+\\}})?(?<file>https?://[^,\t\\[\\;\\{{ ]+)").Match(file).Groups;
                                    if (!string.IsNullOrEmpty(g["file"].Value))
                                        streams.Append(HostStreamProxy(init, g["file"].Value, proxy: proxy), $"{q}p");
                                }

                                etpl.Append(name, title ?? original_title, sArhc, Regex.Match(name, "([0-9]+)").Groups[1].Value, streams.Firts().link, streamquality: streams, vast: init.vast);
                            }
                        }

                        return rjson ? etpl.ToJson() : etpl.ToHtml();
                    }

                }, origsource: origsource, gbcache: !rch.enable);
                #endregion
            }
        }


        #region search
        async ValueTask<Video> search(ProxyManager proxyManager, OnlinesSettings init, string imdb_id, long kinopoisk_id)
        {
            string memKey = $"vibix:view:{kinopoisk_id}:{imdb_id}";

            if (!hybridCache.TryGetValue(memKey, out Video root))
            {
                async Task<Video> goSearch(string imdb_id, long kinopoisk_id)
                {
                    if (string.IsNullOrEmpty(imdb_id) && kinopoisk_id == 0)
                        return null;

                    string uri = kinopoisk_id > 0 ? $"kp/{kinopoisk_id}" : $"imdb/{imdb_id}";
                    var header = httpHeaders(init, HeadersModel.Init(
                        ("Accept", "application/json"),
                        ("Authorization", $"Bearer {init.token}"),
                        ("X-CSRF-TOKEN", "")
                    ));

                    var video = await Http.Get<Video>($"{init.host}/api/v1/publisher/videos/{uri}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: header);

                    if (video == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    if (string.IsNullOrEmpty(video.iframe_url) || string.IsNullOrEmpty(video.type))
                        return null;

                    return video;
                }

                root = await goSearch(null, kinopoisk_id) ?? await goSearch(imdb_id, 0);
                if (root == null)
                    return null;

                proxyManager.Success();
                hybridCache.Set(memKey, root, cacheTime(30, init: init));
            }

            return root;
        }
        #endregion
    }
}
