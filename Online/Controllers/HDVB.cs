using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine.RxEnumerate;
using Shared.Models.Online.HDVB;

namespace Online.Controllers
{
    public class HDVB : BaseOnlineController
    {
        public HDVB() : base(AppInit.conf.HDVB) { }

        [HttpGet]
        [Route("lite/hdvb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1, bool rjson = false, bool similar = false)
        {
            if (similar || kinopoisk_id == 0)
                return await RouteSpiderSearch(title, rjson);

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            reset:

            #region search
            JArray data = await search(kinopoisk_id);
            if (data == null)
            {
                if(init.rhub && init.rhub_fallback)
                {
                    init.rhub = false;
                    goto reset;
                }

                return OnError();
            }
            #endregion

            if (data.First.Value<string>("type") == "movie")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, data.Count);

                foreach (var m in data)
                {
                    string iframe = fixframe(init.corsHost(), m.Value<string>("iframe_url"));
                    string link = $"{host}/lite/hdvb/video?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={HttpUtility.UrlEncode(iframe)}";
                    
                    mtpl.Append(m.Value<string>("translator"), link, "call", accsArgs($"{link.Replace("/video", "/video.m3u8")}&play=true"));
                }

                return await ContentTpl(mtpl);
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

                    return await ContentTpl(tpl);
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

                    var etpl = new EpisodeTpl(vtpl);
                    string iframe = HttpUtility.UrlEncode(fixframe(init.corsHost(), data[t].Value<string>("iframe_url")));
                    string translator = HttpUtility.UrlEncode(data[t].Value<string>("translator"));

                    foreach (int episode in data[t].Value<JArray>("serial_episodes").FirstOrDefault(i => i.Value<int>("season_number") == s).Value<JArray>("episodes").ToObject<List<int>>())
                    {
                        string link = $"{host}/lite/hdvb/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={iframe}&t={translator}&s={s}&e={episode}";
                        string streamlink = accsArgs($"{link.Replace("/serial", "/serial.m3u8")}&play=true");

                        etpl.Append($"{episode} серия", title ?? original_title, s.ToString(), episode.ToString(), link, "call", streamlink: streamlink);
                    }

                    return await ContentTpl(etpl);
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
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            if (rch != null)
            {
                if (rch.IsNotConnected())
                {
                    if (init.rhub_fallback && play)
                        rch.Disabled();
                    else
                        return ContentTo(rch.connectionMsg);
                }

                if (!play && rch.IsRequiredConnected())
                    return ContentTo(rch.connectionMsg);

                if (rch.IsNotSupport(out string rch_error))
                    return ShowError(rch_error);
            }

            return await InvkSemaphore(ipkey($"video:view:video:{iframe}"), async key =>
            {
                if (!hybridCache.TryGetValue(key, out string urim3u8))
                {
                    var header = HeadersModel.Init(
                        ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                        ("sec-fetch-dest", "iframe"),
                        ("sec-fetch-mode", "navigate"),
                        ("sec-fetch-site", "cross-site")
                    );

                    reset:

                    string vid = "vid11", href = null, csrftoken = null, file = null;

                    await httpHydra.GetSpan(iframe, addheaders: header, spanAction: html =>
                    {
                        href = Rx.Match(html, "\"href\":\"([^\"]+)\"");
                        csrftoken = Rx.Match(html, "\"key\":\"([^\"]+)\"")?.Replace("\\", "");
                        
                        file = Rx.Match(html, "\"file\":\"([^\"]+)\"")?.Replace("\\", "");
                        if (file != null)
                        {
                            file = Regex.Replace(file, "^/playlist/", "/");
                            file = Regex.Replace(file, "\\.txt$", "");
                        }

                    });

                    if (!string.IsNullOrWhiteSpace(href) && !string.IsNullOrWhiteSpace(file) && !string.IsNullOrWhiteSpace(csrftoken))
                    {
                        string origin = Regex.Match(iframe, "(https?://[^/]+)").Groups[1].Value;

                        header = HeadersModel.Init(
                            ("accept", "*/*"),
                            ("origin", origin),
                            ("referer", $"{origin}/"),
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "same-site"),
                            ("x-csrf-token", csrftoken)
                        );

                        urim3u8 = await httpHydra.Post($"https://{vid}.{href}/playlist/{file}.txt", "", addheaders: header);

                        if (urim3u8 != null)
                        {
                            if (!urim3u8.Contains("/index.m3u8"))
                            {
                                file = Regex.Match(urim3u8, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                                file = Regex.Replace(file, "^/playlist/", "/");
                                file = Regex.Replace(file, "\\.txt$", "");

                                if (!string.IsNullOrEmpty(file))
                                    urim3u8 = await httpHydra.Post($"https://{vid}.{href}/playlist/{file}.txt", "", addheaders: header);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(urim3u8))
                    {
                        proxyManager?.Refresh();

                        if (init.rhub && init.rhub_fallback)
                        {
                            init.rhub = false;
                            goto reset;
                        }

                        return OnError();
                    }

                    proxyManager?.Success();

                    hybridCache.Set(key, urim3u8, cacheTime(20));
                }

                string m3u8 = HostStreamProxy(urim3u8);

                if (play)
                    return RedirectToPlay(m3u8);

                var headers_stream = init.streamproxy ? null : httpHeaders(init.corsHost(), init.headers_stream);

                return ContentTo(VideoTpl.ToJson("play", m3u8, (title ?? original_title), vast: init.vast, headers: headers_stream));
            });
        }
        #endregion

        #region Serial
        [HttpGet]
        [Route("lite/hdvb/serial")]
        [Route("lite/hdvb/serial.m3u8")]
        async public ValueTask<ActionResult> Serial(string iframe, string t, string s, string e, string title, string original_title, bool play)
        {
            if (await IsRequestBlocked(rch: true, rch_check: false))
                return badInitMsg;

            if (rch != null)
            {
                if (rch.IsNotConnected())
                {
                    if (init.rhub_fallback && play)
                        rch.Disabled();
                    else
                        return ContentTo(rch.connectionMsg);
                }

                if (!play && rch.IsRequiredConnected())
                    return ContentTo(rch.connectionMsg);

                if (rch.IsNotSupport(out string rch_error))
                    return ShowError(rch_error);
            }

            return await InvkSemaphore(ipkey($"video:view:serial:{iframe}:{t}:{s}:{e}"), async key =>
            {
                if (!hybridCache.TryGetValue(key, out string urim3u8))
                {
                    string vid = "vid11";

                    #region playlist
                    string mkey_playlist = $"video:view:playlist:{iframe}";
                    if (!hybridCache.TryGetValue(mkey_playlist, out (List<Folder> playlist, string href, List<HeadersModel> header) cache))
                    {
                        cache.header = HeadersModel.Init(
                            ("accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7"),
                            ("sec-fetch-dest", "iframe"),
                            ("sec-fetch-mode", "navigate"),
                            ("sec-fetch-site", "cross-site")
                        );

                        reset_playlist:

                        string href = null, csrftoken = null, file = null;

                        await httpHydra.GetSpan(iframe, addheaders: cache.header, spanAction: html => 
                        {
                            href = Rx.Match(html, "\"href\":\"([^\"]+)\"");
                            csrftoken = Rx.Match(html, "\"key\":\"([^\"]+)\"")?.Replace("\\", "");
                            
                            file = Rx.Match(html, "\"file\":\"([^\"]+)\"")?.Replace("\\", "");

                            if (file != null)
                            {
                                file = Regex.Replace(file, "^/playlist/", "/");
                                file = Regex.Replace(file, "\\.txt$", "");
                            }
                        });

                        if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(csrftoken))
                        {
                            proxyManager?.Refresh();

                            if (init.rhub && init.rhub_fallback)
                            {
                                init.rhub = false;
                                goto reset_playlist;
                            }

                            return OnError();
                        }

                        string origin = Regex.Match(iframe, "(https?://[^/]+)").Groups[1].Value;

                        cache.header = HeadersModel.Init(
                            ("accept", "*/*"),
                            ("origin", origin),
                            ("referer", $"{origin}/"),
                            ("sec-fetch-dest", "empty"),
                            ("sec-fetch-mode", "cors"),
                            ("sec-fetch-site", "same-site"),
                            ("x-csrf-token", csrftoken)
                        );

                        cache.playlist = await httpHydra.Post<List<Folder>>($"https://{vid}.{href}/playlist/{file}.txt", "", addheaders: cache.header, IgnoreDeserializeObject: true);

                        if (cache.playlist != null && cache.playlist.Count > 0)
                        {
                            cache.href = href;
                            hybridCache.Set(mkey_playlist, cache, cacheTime(40));
                        }
                        else
                        {
                            proxyManager?.Refresh();

                            if (init.rhub && init.rhub_fallback)
                            {
                                init.rhub = false;
                                goto reset_playlist;
                            }

                            return OnError();
                        }
                    }
                    #endregion

                    #region episode
                    if (cache.playlist == null || cache.playlist.Count == 0)
                        return OnError();

                    reset_episode:

                    string episode = cache.playlist.First(i => i.id == s).folder.First(i => i.episode == e).folder.First(i => i.title == t).file;

                    if (!string.IsNullOrEmpty(episode))
                    {
                        episode = Regex.Replace(episode, "^/playlist/", "/");
                        episode = Regex.Replace(episode, "\\.txt$", "");

                        urim3u8 = await httpHydra.Post($"https://{vid}.{cache.href}/playlist/{episode}.txt", "", addheaders: cache.header);
                    }

                    if (string.IsNullOrEmpty(urim3u8) || !urim3u8.Contains("/index.m3u8"))
                    {
                        proxyManager?.Refresh();

                        if (init.rhub && init.rhub_fallback)
                        {
                            init.rhub = false;
                            goto reset_episode;
                        }

                        return OnError();
                    }

                    proxyManager?.Success();

                    hybridCache.Set(key, urim3u8, cacheTime(20));
                    #endregion
                }

                string m3u8 = HostStreamProxy(urim3u8);

                if (play)
                    return Redirect(m3u8);

                var headers_stream = init.streamproxy ? null : httpHeaders(init.corsHost(), init.headers_stream);

                return ContentTo(VideoTpl.ToJson("play", m3u8, (title ?? original_title), vast: init.vast, headers: headers_stream));
            });
        }
        #endregion

        #region SpiderSearch
        [HttpGet]
        [Route("lite/hdvb-search")]
        async public Task<ActionResult> RouteSpiderSearch(string title,bool rjson = false)
        {
            if (string.IsNullOrWhiteSpace(title))
                return OnError();

            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<JArray>($"hdvb:search:{title}", 40, async e =>
            {
                var newheaders = HeadersModel.Init(Http.defaultFullHeaders);
                var root = await httpHydra.Get<JArray>($"{init.cors(init.apihost)}/api/videos.json?token={init.token}&title={HttpUtility.UrlEncode(title)}", safety: true, newheaders: newheaders);

                if (root == null)
                    return e.Fail("results");

                return e.Success(root);
            });

            if (IsRhubFallback(cache, safety: true))
                goto rhubFallback;

            return await ContentTpl(cache, () =>
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

                return stpl;
            });
        }
        #endregion


        #region search
        async ValueTask<JArray> search(long kinopoisk_id)
        {
            string memKey = $"hdvb:view:{kinopoisk_id}";

            if (!hybridCache.TryGetValue(memKey, out JArray root, inmemory: false))
            {
                var newheaders = HeadersModel.Init(Http.defaultFullHeaders);
                root = await httpHydra.Get<JArray>($"{init.cors(init.apihost)}/api/videos.json?token={init.token}&id_kp={kinopoisk_id}", safety: true, newheaders: newheaders);

                if (root == null)
                {
                    proxyManager?.Refresh();
                    return null;
                }

                proxyManager?.Success();

                hybridCache.Set(memKey, root, cacheTime(40), inmemory: false);
            }

            if (root.Count == 0)
                return null;

            return root;
        }
        #endregion


        static string fixframe(string _h, string iframe)
        {
            return Regex.Replace(iframe, "^https?://[^/]+", _h);
        }
    }
}
