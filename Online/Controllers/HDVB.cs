using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.HDVB;

namespace Online.Controllers
{
    public class HDVB : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.HDVB);

        [HttpGet]
        [Route("lite/hdvb")]
        async public ValueTask<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1, bool origsource = false, bool rjson = false, bool similar = false)
        {
            var init = await loadKit(AppInit.conf.HDVB);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (similar || kinopoisk_id == 0)
                return await SpiderSearch(title, origsource, rjson);

            JArray data = await search(kinopoisk_id);
            if (data == null)
                return OnError();

            if (data.First.Value<string>("type") == "movie")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, data.Count);

                foreach (var m in data)
                {
                    string link = $"{host}/lite/hdvb/video?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={HttpUtility.UrlEncode(m.Value<string>("iframe_url"))}";
                    
                    mtpl.Append(m.Value<string>("translator"), link, "call", accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true"));
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    var tpl = new SeasonTpl();
                    var tmp_season = new HashSet<string>();

                    foreach (var voice in data)
                    {
                        foreach (var season in voice.Value<JArray>("serial_episodes"))
                        {
                            string season_name = $"{season.Value<int>("season_number")} сезон";
                            if (tmp_season.Contains(season_name))
                                continue;

                            tmp_season.Add(season_name);

                            string link = $"{host}/lite/hdvb?rjson={rjson}&serial=1&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.Value<int>("season_number")}";
                            tpl.Append(season_name, link, season.Value<int>("season_number"));
                        }
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    #region Перевод
                    var vtpl = new VoiceTpl();

                    for (int i = 0; i < data.Count; i++)
                    {
                        if (data[i].Value<JArray>("serial_episodes").FirstOrDefault(i => i.Value<int>("season_number") == s) == null)
                            continue;

                        if (t == -1)
                            t = i;

                        string link = $"{host}/lite/hdvb?rjson={rjson}&serial=1&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={i}";
                        vtpl.Append(data[i].Value<string>("translator"), t == i, link);
                    }
                    #endregion

                    var etpl = new EpisodeTpl();
                    string iframe = HttpUtility.UrlEncode(data[t].Value<string>("iframe_url"));
                    string translator = HttpUtility.UrlEncode(data[t].Value<string>("translator"));

                    string sArhc = s.ToString();

                    foreach (int episode in data[t].Value<JArray>("serial_episodes").FirstOrDefault(i => i.Value<int>("season_number") == s).Value<JArray>("episodes").ToObject<List<int>>())
                    {
                        string link = $"{host}/lite/hdvb/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={iframe}&t={translator}&s={s}&e={episode}";
                        string streamlink = accsArgs($"{link.Replace("/serial", "/serial.m3u8")}&play=true");

                        etpl.Append($"{episode} серия", title ?? original_title, sArhc, episode.ToString(), link, "call", streamlink: streamlink);
                    }

                    if (rjson)
                        return ContentTo(etpl.ToJson(vtpl));

                    return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                }
                #endregion
            }
        }


        #region Video
        [HttpGet]
        [Route("lite/hdvb/video")]
        [Route("lite/hdvb/video.m3u8")]
        async public ValueTask<ActionResult> Video(string iframe, string title, string original_title, bool play)
        {
            var init = await loadKit(AppInit.conf.HDVB);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxy = proxyManager.Get();

            string memKey = $"video:view:video:{iframe}";
            if (!hybridCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await Http.Get(iframe, referer: $"{init.host}/", timeoutSeconds: 8, proxy: proxy);
                if (html == null)
                    return OnError(proxyManager);

                string vid = "vid1666694269";
                string href = Regex.Match(html, "\"href\":\"([^\"]+)\"").Groups[1].Value;
                string csrftoken = Regex.Match(html, "\"key\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                string file = Regex.Match(html, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(csrftoken))
                    return OnError();

                var header = httpHeaders(init, HeadersModel.Init(
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("origin", $"https://{vid}.{href}"),
                    ("referer", iframe),
                    ("sec-ch-ua", "\"Chromium\";v=\"106\", \"Google Chrome\";v=\"106\", \"Not;A=Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-csrf-token", csrftoken)
                ));

                urim3u8 = await Http.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: header);
                if (urim3u8 == null)
                    return OnError(proxyManager);

                if (!urim3u8.Contains("/index.m3u8"))
                {
                    file = Regex.Match(urim3u8, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                    file = Regex.Replace(file, "^/playlist/", "/");
                    file = Regex.Replace(file, "\\.txt$", "");
                    if (string.IsNullOrWhiteSpace(file))
                        return OnError();

                    urim3u8 = await Http.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: header);
                    if (urim3u8 == null)
                        return OnError(proxyManager);
                }

                proxyManager.Success();
                hybridCache.Set(memKey, urim3u8, cacheTime(20, init: init));
            }

            string m3u8 = HostStreamProxy(init, urim3u8, proxy: proxy);

            if (play)
                return Redirect(m3u8);

            return ContentTo(VideoTpl.ToJson("play", m3u8, (title ?? original_title), vast: init.vast));
        }
        #endregion

        #region Serial
        [HttpGet]
        [Route("lite/hdvb/serial")]
        [Route("lite/hdvb/serial.m3u8")]
        async public ValueTask<ActionResult> Serial(string iframe, string t, string s, string e, string title, string original_title, bool play)
        {
            var init = await loadKit(AppInit.conf.HDVB);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxy = proxyManager.Get();

            string memKey = $"video:view:serial:{iframe}:{t}:{s}:{e}";
            if (!hybridCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await Http.Get(iframe, referer: $"{init.host}/", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (html == null)
                    return OnError(proxyManager);

                #region playlist
                string vid = "vid1666694269";
                string href = Regex.Match(html, "\"href\":\"([^\"]+)\"").Groups[1].Value;
                string csrftoken = Regex.Match(html, "\"key\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                string file = Regex.Match(html, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(csrftoken))
                    return OnError();

                var headers = httpHeaders(init, HeadersModel.Init(
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("origin", $"https://{vid}.{href}"),
                    ("referer", iframe),
                    ("sec-ch-ua", "\"Chromium\";v=\"106\", \"Google Chrome\";v=\"106\", \"Not;A=Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-csrf-token", csrftoken)
                ));

                var playlist = await Http.Post<List<Folder>>($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: headers, IgnoreDeserializeObject: true);
                if (playlist == null || playlist.Count == 0)
                    return OnError(proxyManager);
                #endregion

                file = playlist.First(i => i.id == s).folder.First(i => i.episode == e).folder.First(i => i.title == t).file;
                if (string.IsNullOrWhiteSpace(file))
                    return OnError();

                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                urim3u8 = await Http.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: headers);
                if (urim3u8 == null || !urim3u8.Contains("/index.m3u8"))
                    return OnError(proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, urim3u8, cacheTime(20, init: init));
            }

            if (play)
                return Redirect(HostStreamProxy(init, urim3u8, proxy: proxy));

            return Content("{\"method\":\"play\",\"url\":\"" + HostStreamProxy(init, urim3u8, proxy: proxy) + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region SpiderSearch
        [HttpGet]
        [Route("lite/hdvb-search")]
        async public ValueTask<ActionResult> SpiderSearch(string title, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.HDVB);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var cache = await InvokeCache<JArray>($"hdvb:search:{title}", cacheTime(40, init: init), proxyManager, async res =>
            {
                var root = await Http.Get<JArray>($"{init.host}/api/videos.json?token={init.token}&title={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null)
                    return res.Fail("results");

                return root;
            });

            return OnResult(cache, () =>
            {
                var hash = new HashSet<long>(cache.Value.Count);
                var stpl = new SimilarTpl(cache.Value.Count);

                foreach (var j in cache.Value)
                {
                    var kinopoisk_id = j.Value<long?>("kinopoisk_id");
                    if (kinopoisk_id > 0 && !hash.Contains((long)kinopoisk_id))
                    {
                        hash.Add((long)kinopoisk_id);
                        string uri = $"{host}/lite/hdvb?kinopoisk_id={kinopoisk_id}";
                        stpl.Append(j.Value<string>("title_ru") ?? j.Value<string>("title_en"), j.Value<int>("year").ToString(), string.Empty, uri, PosterApi.Size(j.Value<string>("poster")));
                    }
                }

                return rjson ? stpl.ToJson() : stpl.ToHtml();

            }, origsource: origsource);
        }
        #endregion


        #region search
        async ValueTask<JArray> search(long kinopoisk_id)
        {
            string memKey = $"hdvb:view:{kinopoisk_id}";

            if (!hybridCache.TryGetValue(memKey, out JArray root))
            {
                var init = await loadKit(AppInit.conf.HDVB);

                root = await Http.Get<JArray>($"{init.host}/api/videos.json?token={init.token}&id_kp={kinopoisk_id}", timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null)
                {
                    proxyManager.Refresh();
                    return null;
                }

                proxyManager.Success();
                hybridCache.Set(memKey, root, cacheTime(40, init: init));
            }

            if (root.Count == 0)
                return null;

            return root;
        }
        #endregion
    }
}
