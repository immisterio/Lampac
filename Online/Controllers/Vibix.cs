using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Vibix;

namespace Online.Controllers
{
    public class Vibix : BaseOnlineController
    {
        public Vibix() : base(AppInit.conf.Vibix) { }

        [HttpGet]
        [Route("lite/vibix")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int s = -1, bool rjson = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            var data = await search(imdb_id, kinopoisk_id);
            if (data == null)
                return OnError();

            rhubFallback:
            var cache = await InvokeCacheResult<EmbedModel>(rch.ipkey($"vibix:iframe:{data.iframe_url}:{init.token}", proxyManager), 20, async e =>
            {
                string api_url = data.iframe_url
                    .Replace("/embed/", "/api/v1/embed/")
                    .Replace("/embed-serials/", "/api/v1/embed-serials/");

                api_url += $"?iframe_url={HttpUtility.UrlEncode(data.iframe_url)}";
                api_url += $"&kp={CrypTo.unic(6).ToLower()}";

                var api_headers = HeadersModel.Init(
                    ("accept", "*/*"),
                    ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("referer", data.iframe_url)
                );

                var root = await httpHydra.Get<JObject>(api_url, addheaders: api_headers);

                if (root == null || !root.ContainsKey("data") || root["data"]?["playlist"] == null)
                    return e.Fail("root", refresh_proxy: true);

                return e.Success(new EmbedModel() { playlist = root["data"]["playlist"].ToObject<Seasons[]>() });
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

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
                                streams.Append(HostStreamProxy(g["file"].Value), $"{q}p");
                        }

                        mtpl.Append(movie.title, streams.Firts().link, streamquality: streams, vast: init.vast);
                    }

                    return mtpl;
                });
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

                        return tpl;
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
                                        streams.Append(HostStreamProxy(g["file"].Value), $"{q}p");
                                }

                                etpl.Append(name, title ?? original_title, sArhc, Regex.Match(name, "([0-9]+)").Groups[1].Value, streams.Firts().link, streamquality: streams, vast: init.vast);
                            }
                        }

                        return etpl;
                    }
                });
                #endregion
            }
        }


        #region search
        async ValueTask<Video> search(string imdb_id, long kinopoisk_id)
        {
            string memKey = $"vibix:view:{kinopoisk_id}:{imdb_id}";

            if (!hybridCache.TryGetValue(memKey, out Video root))
            {
                async Task<Video> goSearch(string imdb_id, long kinopoisk_id)
                {
                    if (string.IsNullOrEmpty(imdb_id) && kinopoisk_id == 0)
                        return null;

                    string uri = kinopoisk_id > 0 ? $"kp/{kinopoisk_id}" : $"imdb/{imdb_id}";

                    var video = await httpHydra.Get<Video>($"{init.host}/api/v1/publisher/videos/{uri}", addheaders: HeadersModel.Init(
                        ("Accept", "application/json"),
                        ("Authorization", $"Bearer {init.token}"),
                        ("X-CSRF-TOKEN", "")
                    ));

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
                hybridCache.Set(memKey, root, cacheTime(30));
            }

            return root;
        }
        #endregion
    }
}
