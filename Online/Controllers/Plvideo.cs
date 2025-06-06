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
        async public Task<ActionResult> Index(string title, string original_title, int year, int serial, bool rjson = false)
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
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

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
                    var mtpl = new MovieTpl(title, original_title, cache.Value.Count);

                    foreach (var movie in cache.Value)
                    {
                        string name = StringConvert.SearchName(movie.Value<string>("title"));
                        if (name != null && name.StartsWith(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                        {
                            long duration = movie["uploadFile"].Value<long>("videoDuration");
                            if (duration > 1900000) // 30 minutes
                            {
                                if (name.Contains("трейлер") || name.Contains("премьера") || name.Contains("сезон") || name.Contains("сериал") || name.Contains("серия") || name.Contains("серий"))
                                    continue;

                                if (movie.Value<string>("visible") != "public")
                                    continue;

                                mtpl.Append(movie.Value<string>("title"), $"{host}/lite/plvideo/movie?linkid={movie.Value<string>("id")}", "call");
                            }
                        }
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, gbcache: !rch.enable);
            }
        }



        [HttpGet]
        [Route("lite/plvideo/movie")]
        async public Task<ActionResult> Movie(string linkid)
        {
            var init = await loadKit(AppInit.conf.Plvideo);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(linkid))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var cache = await InvokeCache<JObject>($"plvideo:play:{linkid}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
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
                if (cache.Value[q] is JObject jq && jq.ContainsKey("hls"))
                {
                    string hls = jq.Value<string>("hls");
                    if (!string.IsNullOrEmpty(hls))
                        streams.Append(HostStreamProxy(init, hls + "#.m3u8", proxy: proxy), q);
                }
            }

            return ContentTo(VideoTpl.ToJson("play", streams.Firts().link, streams.Firts().quality, streamquality: streams, vast: init.vast));
        }
    }
}
