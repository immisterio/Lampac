using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Online.Controllers
{
    public class CDNvideohub : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/cdnvideohub")]
        async public ValueTask<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int s = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.CDNvideohub);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            reset:
            var cache = await InvokeCache<JObject>($"cdnvideohub:view:{kinopoisk_id}", cacheTime(30, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/api/v1/player/sv/playlist?pub=12&aggr=kp&id={kinopoisk_id}";

                var root = rch.enable 
                    ? await rch.Get<JObject>(uri, httpHeaders(init)) 
                    : await Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));

                if (root == null || !root.ContainsKey("items"))
                    return res.Fail("root");

                var videos = root["items"] as JArray;
                if (videos == null || videos.Count == 0)
                    return res.Fail("video");

                return root;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => 
            {
                if (cache.Value.Value<bool>("isSerial"))
                {
                    #region Сериал
                    string defaultargs = $"&rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}";

                    if (s == -1)
                    {
                        #region Сезоны
                        var tpl = new SeasonTpl();
                        var hash = new HashSet<int>();

                        foreach (var video in cache.Value["items"].OrderBy(i => i.Value<int>("season")))
                        {
                            int season = video.Value<int>("season");

                            if (hash.Contains(season))
                                continue;

                            hash.Add(season);
                            tpl.Append($"{season} сезон", $"{host}/lite/cdnvideohub?s={season}{defaultargs}", season);
                        }

                        return rjson ? tpl.ToJson() : tpl.ToHtml();
                        #endregion
                    }
                    else
                    {
                        #region Перевод
                        var vtpl = new VoiceTpl();
                        var tmpVoice = new HashSet<string>();

                        foreach (var video in cache.Value["items"])
                        {
                            if (video.Value<int>("season") != s)
                                continue;

                            string voice_studio = video.Value<string>("voiceStudio");
                            if (string.IsNullOrEmpty(voice_studio) || tmpVoice.Contains(voice_studio))
                                continue;

                            tmpVoice.Add(voice_studio);

                            if (string.IsNullOrEmpty(t))
                                t = voice_studio;

                            vtpl.Append(voice_studio, t == voice_studio, $"{host}/lite/cdnvideohub?s={s}&t={HttpUtility.UrlEncode(voice_studio)}{defaultargs}");
                        }
                        #endregion

                        var etpl = new EpisodeTpl();
                        string sArhc = s.ToString();
                        var tmpEpisode = new HashSet<int>();

                        foreach (var video in cache.Value["items"].OrderBy(i => i.Value<int>("episode")))
                        {
                            if (video.Value<int>("season") != s || video.Value<string>("voiceStudio") != t)
                                continue;

                            string vkId = video.Value<string>("vkId");
                            if (string.IsNullOrEmpty(vkId))
                                continue;

                            int episode = video.Value<int>("episode");

                            if (tmpEpisode.Contains(episode))
                                continue;

                            tmpEpisode.Add(episode);

                            string link = accsArgs($"{host}/lite/cdnvideohub/video.m3u8?vkId={vkId}&title={HttpUtility.UrlEncode(title)}");

                            etpl.Append($"{episode} серия", title ?? original_title, sArhc, episode.ToString(), link, "call", streamlink: $"{link}&play=true", vast: init.vast);
                        }

                        if (rjson)
                            return etpl.ToJson(vtpl);

                        return vtpl.ToHtml() + etpl.ToHtml();
                    }
                    #endregion
                }
                else
                {
                    #region Фильм
                    var mtpl = new MovieTpl(title, original_title);

                    foreach (var video in cache.Value["items"])
                    {
                        string voice = video.Value<string>("voiceStudio") ?? video.Value<string>("voiceType");
                        string vkId = video.Value<string>("vkId");

                        string link = accsArgs($"{host}/lite/cdnvideohub/video.m3u8?vkId={vkId}&title={HttpUtility.UrlEncode(title)}");

                        mtpl.Append(voice, link, "call", vast: init.vast);
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                    #endregion
                }

            }, origsource: origsource, gbcache: !rch.enable);
        }


        #region Video
        [HttpGet]
        [Route("lite/cdnvideohub/video.m3u8")]
        async public ValueTask<ActionResult> Video(string vkId, string title, bool play)
        {
            var init = await loadKit(AppInit.conf.CDNvideohub);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo, keepalive: -1);
            if (rch.IsNotSupport("cors,web", out string rch_error))
                return ShowError(rch_error);

            if (rch.IsNotConnected() && init.rhub_fallback && play)
                rch.Disabled();

            var cache = await InvokeCache<string>(rch.ipkey($"cdnvideohub:video:{vkId}", proxyManager), cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/api/v1/player/sv/video/{vkId}";

                string iframe;
                if (rch.enable)
                {
                    iframe = await rch.Get(init.cors(uri), headers: httpHeaders(init));
                }
                else
                {
                    iframe = await Http.Get(uri, timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init), httpversion: 2);
                }

                if (iframe == null)
                    return res.Fail("iframe");

                string hls = Regex.Match(iframe, "\"hlsUrl\":\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(hls))
                    return res.Fail("hls");

                return hls.Replace("u0026", "&").Replace("\\", "");
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg, gbcache: !rch.enable);

            string link = HostStreamProxy(init, cache.Value, proxy: proxyManager.Get());

            if (play)
                return RedirectToPlay(link);

            return ContentTo(VideoTpl.ToJson("play", link, title, vast: init.vast));
        }
        #endregion
    }
}
