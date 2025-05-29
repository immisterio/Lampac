using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Templates;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers.LITE
{
    public class Plvideo : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/plvideo")]
        async public Task<ActionResult> Index(string title, int year, int serial, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Plvideo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            string searchTitle = StringConvert.SearchName(title);
            if (string.IsNullOrEmpty(searchTitle) || year == 0)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();
            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);

            if (serial == 1)
            {
                return OnError();
            }
            else
            {
                var cache = await InvokeCache<JArray>($"plvideo:view:{searchTitle}:{year}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string uri = $"v1/videos?Type=video&Query={HttpUtility.UrlEncode($"{title} {year}")}&From=0&Size=20&Aud=16&Qf=false";
                    var root = rch.enable ? await rch.Get<JObject>($"{init.host}/{uri}", httpHeaders(init)) : await HttpClient.Get<JObject>($"{init.host}/{uri}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                    if (root == null || !root.ContainsKey("items"))
                        return res.Fail("content");

                    return root["items"].ToObject<JArray>();
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                return OnResult(cache, () =>
                {
                    var mtpl = new MovieTpl(title);

                    foreach (var movie in cache.Value)
                    {
                        string name = StringConvert.SearchName(movie.Value<string>("title"));
                        if (name != null && name.StartsWith(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                        {
                            long duration = movie["uploadFile"].Value<long>("videoDuration");
                            if (duration > 170000)
                            {
                                if (name.Contains("ענויכונ") || name.Contains("ןנולונא") || name.Contains("סוחמם") || name.Contains("סונטאכ") || name.Contains("סונט") || name.Contains("סונטי"))
                                    continue;

                                if (movie.Value<string>("visible") != "public")
                                    continue;

                                string file = $"{host}/lite/plvideo/movie.m3u8?linkid={movie.Value<string>("id")}";
                                mtpl.Append(movie.Value<string>("title"), file, "call", accsArgs($"{file}&play=true"));
                            }
                        }
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, gbcache: !rch.enable);
            }
        }



        [HttpGet]
        [Route("lite/plvideo/movie.m3u8")]
        async public Task<ActionResult> Movie(string linkid, bool play)
        {
            var init = await loadKit(AppInit.conf.Plvideo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(linkid))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();
            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);

            var cache = await InvokeCache<JObject>($"plvideo:play:{linkid}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                string uri = $"v1/videos/{linkid}?Aud=16";
                var root = rch.enable ? await rch.Get<JObject>($"{init.host}/{uri}", httpHeaders(init)) : await HttpClient.Get<JObject>($"{init.host}/{uri}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (root == null || !root.ContainsKey("item"))
                    return res.Fail("item");

                return root["item"].Value<JObject>("profiles");
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            var streams = new StreamQualityTpl();
            foreach (string q in new string[] { "2160p", "1440p", "1080p", "720p", "468p", "360p", "240p" })
            {
                if (cache.Value.ContainsKey(q))
                {
                    string hls = cache.Value[q].Value<string>("hls");
                    if (!string.IsNullOrEmpty(hls))
                        streams.Append(HostStreamProxy(init, hls + "#.m3u8", proxy: proxy), q);
                }
            }

            if (play)
                return Redirect(streams.Firts().link);

            return ContentTo(VideoTpl.ToJson("play", streams.Firts().link, streams.Firts().quality, streamquality: streams, vast: init.vast));
        }
    }
}
