using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Templates;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers.LITE
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

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var cache = await InvokeCache<JObject>(rch.ipkey($"cdnvideohub:view:{kinopoisk_id}", proxyManager), cacheTime(20, rhub: 2, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"{init.corsHost()}/api/v1/player/sv?pub=27&id={kinopoisk_id}&aggr=kp";
                var root = rch.enable ? await rch.Get<JObject>(uri, httpHeaders(init)) : await HttpClient.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (root == null || !root.ContainsKey("video"))
                    return res.Fail("root");

                var videos = root["video"] as JArray;
                if (videos == null || videos.Count == 0)
                    return res.Fail("video");

                return root;
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => 
            {
                if (cache.Value.Value<bool>("is_serial"))
                {
                    #region Сериал
                    string defaultargs = $"&rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}";

                    if (s == -1)
                    {
                        #region Сезоны
                        var tpl = new SeasonTpl();
                        var hash = new HashSet<int>();

                        foreach (var video in cache.Value["video"])
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

                        foreach (var video in cache.Value["video"])
                        {
                            if (video.Value<int>("season") != s)
                                continue;

                            string voice_studio = video.Value<string>("voice_studio");
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

                        foreach (var video in cache.Value["video"].OrderBy(i => i.Value<int>("episode")))
                        {
                            if (video.Value<int>("season") != s || video.Value<string>("voice_studio") != t)
                                continue;

                            string hls = video.Value<JObject>("sources").Value<string>("hlsUrl");
                            int episode = video.Value<int>("episode");

                            etpl.Append($"{episode} серия", title ?? original_title, sArhc, episode.ToString(), HostStreamProxy(init, hls, proxy: proxy), vast: init.vast);
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

                    foreach (var video in cache.Value["video"])
                    {
                        string voice = video.Value<string>("voice_studio") ?? video.Value<string>("voice_type");
                        string hls = video.Value<JObject>("sources").Value<string>("hlsUrl");

                        mtpl.Append(voice, HostStreamProxy(init, hls, proxy: proxy), vast: init.vast);
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();
                    #endregion
                }

            }, origsource: origsource, gbcache: !rch.enable);
        }
    }
}
