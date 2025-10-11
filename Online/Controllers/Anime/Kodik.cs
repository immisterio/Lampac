using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Kodik;
using Shared.Models.Online.Settings;
using System.Security.Cryptography;
using System.Text;

namespace Online.Controllers
{
    public class Kodik : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.Kodik);

        #region database
        static List<Result> databaseCache;

        static IEnumerable<Result> database
        {
            get
            {
                if (AppInit.conf.multiaccess || databaseCache != null)
                    return databaseCache ??= JsonHelper.ListReader<Result>("data/kodik.json", 70_000);

                return JsonHelper.IEnumerableReader<Result>("data/kodik.json");
            }
        }
        #endregion

        #region InitKodikInvoke
        public KodikInvoke InitKodikInvoke(KodikSettings init)
        {
            var proxy = proxyManager.Get();

            return new KodikInvoke
            (
                host,
                init.apihost,
                init.token,
                init.hls,
                init.cdn_is_working,
                "video",
                database,
                (uri, head) => Http.Get(init.cors(uri), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                (uri, data) => Http.Post(init.cors(uri), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
                streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
                requesterror: () => proxyManager.Refresh()
            );
        }
        #endregion

        #region Initialization
        ValueTask<KodikSettings> Initialization()
        {
            return loadKit(AppInit.conf.Kodik, (j, i, c) =>
            {
                if (j.ContainsKey("linkhost"))
                    i.linkhost = c.linkhost;

                if (j.ContainsKey("secret_token"))
                    i.secret_token = c.secret_token;

                if (j.ContainsKey("auto_proxy"))
                    i.auto_proxy = c.auto_proxy;

                if (j.ContainsKey("cdn_is_working"))
                    i.cdn_is_working = c.cdn_is_working;

                return i;
            });
        }
        #endregion

        [HttpGet]
        [Route("lite/kodik")]
        async public ValueTask<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string pick, string kid, int s = -1, bool rjson = false, bool similar = false)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: init.rhub && init.rhub_fallback))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            List<Result> content = null;
            var oninvk = InitKodikInvoke(init);

            if (similar || clarification == 1 || (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
            {
                EmbedModel res = null;

                if (clarification == 1)
                {
                    if (string.IsNullOrEmpty(title))
                        return OnError();

                    res = await InvokeCache($"kodik:search:{title}", cacheTime(40, init: init), () => oninvk.Embed(title, null, clarification), proxyManager);
                    if (res?.result == null || res.result.Count == 0)
                        return OnError();
                }
                else
                {
                    if (string.IsNullOrEmpty(pick) && string.IsNullOrEmpty(title ?? original_title))
                        return OnError();

                    res = await InvokeCache($"kodik:search2:{original_title}:{title}:{clarification}", cacheTime(40, init: AppInit.conf.Kodik), async () => 
                    {
                        var i = await oninvk.Embed(null, original_title, clarification);
                        if (i?.result == null || i.result.Count == 0)
                            return await oninvk.Embed(title, null, clarification);

                        return i;

                    }, proxyManager);
                }

                if (string.IsNullOrEmpty(pick))
                    return ContentTo(res?.stpl == null ? string.Empty : (rjson ? res.stpl.Value.ToJson() : res.stpl.Value.ToHtml()));

                content = oninvk.Embed(res.result, pick);
            }
            else
            {
                content = await InvokeCache($"kodik:search:{kinopoisk_id}:{imdb_id}", cacheTime(40, init: AppInit.conf.Kodik), () => oninvk.Embed(imdb_id, kinopoisk_id, s), proxyManager);
                if (content == null || content.Count == 0)
                    return LocalRedirect(accsArgs($"/lite/kodik?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}"));
            }

            return ContentTo(await oninvk.Html(content, accsArgs(string.Empty), imdb_id, kinopoisk_id, title, original_title, clarification, pick, kid, s, true, rjson));
        }

        #region Video
        [HttpGet]
        [Route("lite/kodik/video")]
        [Route("lite/kodik/video.m3u8")]
        async public ValueTask<ActionResult> VideoAPI(string title, string original_title, string link, int episode, bool play)
        {
            var init = await Initialization();
            if (await IsBadInitialization(init, rch: init.rhub && init.rhub_fallback))
                return badInitMsg;

            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            if (string.IsNullOrWhiteSpace(init.secret_token))
            {
                var oninvk = InitKodikInvoke(init);

                var streams = await InvokeCache($"kodik:video:{link}:{play}", cacheTime(40, init: init), () => oninvk.VideoParse(init.linkhost, link), proxyManager);
                if (streams == null)
                    return OnError();

                string result = oninvk.VideoParse(streams, title, original_title, episode, play, vast: init.vast);
                if (string.IsNullOrEmpty(result))
                    return OnError();

                if (play)
                    return RedirectToPlay(result);

                return ContentTo(result);
            }
            else
            {
                string userIp = requestInfo.IP;
                if (init.localip)
                {
                    userIp = await mylocalip();
                    if (userIp == null)
                        return OnError();
                }

                var proxy = proxyManager.Get();

                string memKey = $"kodik:view:stream:{link}:{init.secret_token}:{requestInfo.IP}";

                return await InvkSemaphore(init, memKey, async () =>
                {
                    if (!hybridCache.TryGetValue(memKey, out (List<(string q, string url)> streams, SegmentTpl segments) cache))
                    {
                        string deadline = DateTime.Now.AddHours(2).ToString("yyyy MM dd HH").Replace(" ", "");
                        string hmac = HMAC(init.secret_token, $"{link}:{userIp}:{deadline}");

                        var root = await Http.Get<JObject>($"http://kodik.biz/api/video-links?link={link}&p={init.token}&ip={userIp}&d={deadline}&s={hmac}&auto_proxy={init.auto_proxy.ToString().ToLower()}&skip_segments=true", timeoutSeconds: 8, proxy: proxy);

                        if (root == null || !root.ContainsKey("links"))
                            return OnError("links");

                        cache.streams = new List<(string q, string url)>(3);

                        foreach (var link in root["links"].ToObject<Dictionary<string, JObject>>())
                        {
                            string src = link.Value.Value<string>("Src");
                            if (src.StartsWith("http"))
                                src = src.Substring(src.IndexOf("://") + 3);

                            cache.streams.Add(($"{link.Key}p", $"https://{src}"));
                        }

                        if (cache.streams.Count == 0)
                        {
                            proxyManager.Refresh();
                            return OnError("streams");
                        }

                        cache.streams.Reverse();

                        if (root.ContainsKey("segments"))
                        {
                            var segs = root["segments"] as JObject;
                            if (segs != null)
                            {
                                cache.segments = new SegmentTpl();

                                foreach (string key in new string[] { "ad", "skip" })
                                {
                                    if (segs.ContainsKey(key))
                                    {
                                        var arr = segs[key] as JArray;
                                        if (arr != null)
                                        {
                                            foreach (var it in arr)
                                            {
                                                int? s = it.Value<int?>("start");
                                                int? e = it.Value<int?>("end");
                                                if (s.HasValue && e.HasValue)
                                                    cache.segments.ad(s.Value, e.Value);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        proxyManager.Success();
                        hybridCache.Set(memKey, cache, cacheTime(20, init: init));
                    }

                    var streamquality = new StreamQualityTpl();
                    foreach (var l in cache.streams)
                        streamquality.Append(HostStreamProxy(init, l.url, proxy: proxy), l.q);

                    if (play)
                        return RedirectToPlay(streamquality.Firts().link);

                    string name = title ?? original_title;
                    if (episode > 0)
                        name += $" ({episode} серия)";

                    return ContentTo(VideoTpl.ToJson("play", streamquality.Firts().link, name, streamquality: streamquality, vast: init.vast, segments: cache.segments));
                });
            }
        }
        #endregion


        #region HMAC
        static string HMAC(string key, string message)
        {
            using (var hash = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "").ToLower();
            }
        }
        #endregion
    }
}
