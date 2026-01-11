using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine.RxEnumerate;

namespace Online.Controllers
{
    public class CDNvideohub : BaseOnlineController
    {
        public CDNvideohub() : base(AppInit.conf.CDNvideohub) { }

        [HttpGet]
        [Route("lite/cdnvideohub")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, string t, int s = -1, bool rjson = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            rhubFallback:
            var cache = await InvokeCacheResult<JObject>($"cdnvideohub:view:{kinopoisk_id}", 30, async e =>
            {
                var root = await httpHydra.Get<JObject>($"{init.corsHost()}/api/v1/player/sv/playlist?pub=12&aggr=kp&id={kinopoisk_id}");

                if (root == null || !root.ContainsKey("items"))
                    return e.Fail("root", refresh_proxy: true);

                var videos = root["items"] as JArray;
                if (videos == null || videos.Count == 0)
                    return e.Fail("video");

                return e.Success(root);
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            return await ContentTpl(cache, () => 
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

                        return tpl;
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

                        var etpl = new EpisodeTpl(vtpl);
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

                            etpl.Append($"{episode} серия", title ?? original_title, s.ToString(), episode.ToString(), link, "call", streamlink: $"{link}&play=true", vast: init.vast);
                        }

                        return etpl;
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

                    return mtpl;
                    #endregion
                }
            });
        }


        #region Video
        [HttpGet]
        [Route("lite/cdnvideohub/video.m3u8")]
        async public ValueTask<ActionResult> Video(string vkId, string title, bool play)
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

            rhubFallback:

            var cache = await InvokeCacheResult<string>(ipkey($"cdnvideohub:video:{vkId}"), 20, async e =>
            {
                string hls = null;

                await httpHydra.GetSpan($"{init.corsHost()}/api/v1/player/sv/video/{vkId}", iframe => 
                {
                    hls = Rx.Match(iframe, "\"hlsUrl\":\"([^\"]+)\"");
                });

                if (string.IsNullOrEmpty(hls))
                    return e.Fail("hls", refresh_proxy: true);

                return e.Success(hls.Replace("u0026", "&").Replace("\\", ""));
            });

            if (IsRhubFallback(cache))
                goto rhubFallback;

            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            string link = HostStreamProxy(cache.Value);

            if (play)
                return RedirectToPlay(link);

            return ContentTo(VideoTpl.ToJson("play", link, title, vast: init.vast));
        }
        #endregion
    }
}
