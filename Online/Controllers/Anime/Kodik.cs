using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Kodik;
using Shared.Models.Online.Settings;
using System.Security.Cryptography;
using System.Text;

namespace Online.Controllers
{
    public class Kodik : BaseOnlineController<KodikSettings>
    {
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

        #region HMAC
        static string HMAC(string key, string message)
        {
            using (var hash = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "").ToLower();
            }
        }
        #endregion


        #region KodikInvoke
        KodikInvoke oninvk;

        public Kodik() : base(AppInit.conf.Kodik) 
        {
            loadKitInitialization = (j, i, c) =>
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
            };

            requestInitialization = () =>
            {
                oninvk = new KodikInvoke
                (
                    host,
                    init,
                    "video",
                    database,
                    (uri, head, safety) => httpHydra.Get(uri, safety: safety),
                    (uri, data) => httpHydra.Post(uri, data),
                    streamfile => HostStreamProxy(streamfile),
                    requesterror: () => proxyManager?.Refresh()
                );
            };
        }
        #endregion

        [HttpGet]
        [Route("lite/kodik")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int clarification, string pick, string kid, int s = -1, bool rjson = false, bool similar = false)
        {
            if (await IsRequestBlocked(rch: true))
                return badInitMsg;

            List<Result> content = null;

            if (similar || clarification == 1 || (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
            {
                EmbedModel res = null;

                if (clarification == 1)
                {
                    if (string.IsNullOrEmpty(title))
                        return OnError();

                    res = await InvokeCache($"kodik:search:{title}", 40, () => oninvk.Embed(title, null, clarification));
                    if (res?.result == null || res.result.Count == 0)
                        return OnError();
                }
                else
                {
                    if (string.IsNullOrEmpty(pick) && string.IsNullOrEmpty(title ?? original_title))
                        return OnError();

                    res = await InvokeCache($"kodik:search2:{original_title}:{title}:{clarification}", 40, async () => 
                    {
                        var i = await oninvk.Embed(null, original_title, clarification);
                        if (i?.result == null || i.result.Count == 0)
                            return await oninvk.Embed(title, null, clarification);

                        return i;

                    });
                }

                if (string.IsNullOrEmpty(pick))
                    return await ContentTpl(res?.stpl);

                content = oninvk.Embed(res.result, pick);
            }
            else
            {
                content = await InvokeCache($"kodik:search:{kinopoisk_id}:{imdb_id}", 40, () => oninvk.Embed(imdb_id, kinopoisk_id, s));
                if (content == null || content.Count == 0)
                    return LocalRedirect(accsArgs($"/lite/kodik?rjson={rjson}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}"));
            }

            return await ContentTpl(await oninvk.Tpl(content, accsArgs(string.Empty), imdb_id, kinopoisk_id, title, original_title, clarification, pick, kid, s, true, rjson));
        }

        #region Video
        [HttpGet]
        [Route("lite/kodik/video")]
        [Route("lite/kodik/video.m3u8")]
        async public ValueTask<ActionResult> VideoAPI(string title, string original_title, string link, int episode, bool play)
        {
            if (await IsRequestBlocked(rch: true, rch_check: !play))
                return badInitMsg;

            if (string.IsNullOrWhiteSpace(init.secret_token))
            {
                var streams = await InvokeCache($"kodik:video:{link}:{play}", 40, () => oninvk.VideoParse(init.linkhost, link));
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

                return await InvkSemaphore($"kodik:view:stream:{link}:{init.secret_token}:{requestInfo.IP}", async key =>
                {
                    if (!hybridCache.TryGetValue(key, out (List<(string q, string url)> streams, SegmentTpl segments) cache))
                    {
                        string deadline = DateTime.Now.AddHours(4).ToString("yyyy MM dd HH").Replace(" ", "");
                        string hmac = HMAC(init.secret_token, $"{link}:{userIp}:{deadline}");

                        string uri = $"http://kodik.biz/api/video-links?link={link}&p={init.token}&ip={userIp}&d={deadline}&s={hmac}&auto_proxy={init.auto_proxy.ToString().ToLower()}&skip_segments=true";
                        
                        var root = await httpHydra.Get<JObject>(uri, safety: true);

                        if (root == null || !root.ContainsKey("links"))
                            return OnError("links", refresh_proxy: true);

                        cache.streams = new List<(string q, string url)>(3);

                        foreach (var link in root["links"].ToObject<Dictionary<string, JObject>>())
                        {
                            string src = link.Value.Value<string>("Src");
                            if (src.StartsWith("http"))
                                src = src.Substring(src.IndexOf("://") + 3);

                            cache.streams.Add(($"{link.Key}p", $"https://{src}"));
                        }

                        if (cache.streams.Count == 0)
                            return OnError("streams", refresh_proxy: true);

                        cache.streams.Reverse();

                        if (root.ContainsKey("segments"))
                        {
                            var segs = root["segments"] as JObject;
                            if (segs != null)
                            {
                                cache.segments = new SegmentTpl();

                                foreach (string segmentkey in new string[] { "ad", "skip" })
                                {
                                    if (segs.ContainsKey(segmentkey))
                                    {
                                        var arr = segs[segmentkey] as JArray;
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

                        proxyManager?.Success();
                        hybridCache.Set(key, cache, cacheTime(120));
                    }

                    var streamquality = new StreamQualityTpl();
                    foreach (var l in cache.streams)
                        streamquality.Append(HostStreamProxy(l.url), l.q);

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
    }
}
