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
    public class RutubeMovie : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/rutubemovie")]
        async public ValueTask<ActionResult> Index(string title, string original_title, int year, int serial, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.RutubeMovie);
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
                var cache = await InvokeCache<JArray>($"rutubemovie:view:{searchTitle}:{year}", cacheTime(40, init: init), rch.enable ? null : proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    string uri = $"api/search/video/?content_type=video&duration=movie&query={HttpUtility.UrlEncode($"{title} {year}")}";
                    var root = rch.enable ? await rch.Get<JObject>($"{init.host}/{uri}", httpHeaders(init)) : await HttpClient.Get<JObject>($"{init.host}/{uri}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                    if (root == null || !root.ContainsKey("results"))
                        return res.Fail("content");

                    return root["results"].ToObject<JArray>();
                });

                if (IsRhubFallback(cache, init))
                    goto reset;

                return OnResult(cache, () =>
                {
                    var mtpl = new MovieTpl(title, original_title, cache.Value.Count);

                    foreach (var movie in cache.Value)
                    {
                        string name = StringConvert.SearchName(movie.Value<string>("title"));
                        if (name != null && name.Contains(searchTitle) && (name.Contains(year.ToString()) || name.Contains((year + 1).ToString()) || name.Contains((year - 1).ToString())))
                        {
                            long duration = movie.Value<long>("duration");
                            if (duration > 1800) // 30 minutes
                            {
                                if (name.Contains("трейлер") || name.Contains("премьера") || name.Contains("сезон") || name.Contains("сериал") || name.Contains("серия") || name.Contains("серий"))
                                    continue;

                                if (movie["category"].Value<int>("id") == 4)
                                {
                                    if (movie.Value<bool>("is_hidden") || movie.Value<bool>("is_deleted") || movie.Value<bool>("is_adult") || movie.Value<bool>("is_locked") || movie.Value<bool>("is_audio") || movie.Value<bool>("is_paid") || movie.Value<bool>("is_livestream"))
                                        continue;

                                    mtpl.Append(movie.Value<string>("title"), $"{host}/lite/rutubemovie/play?linkid={movie.Value<string>("id")}", "call", vast: init.vast);
                                }
                            }
                        }
                    }

                    return rjson ? mtpl.ToJson() : mtpl.ToHtml();

                }, gbcache: !rch.enable);
            }
        }


        [HttpGet]
        [Route("lite/rutubemovie/play")]
        async public ValueTask<ActionResult> Movie(string linkid)
        {
            var init = await loadKit(AppInit.conf.RutubeMovie);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(linkid))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var cache = await InvokeCache<string>($"rutubemovie:play:{linkid}", cacheTime(20, init: init), rch.enable ? null : proxyManager, async res =>
            {
                string uri = $"api/play/options/{linkid}/?no_404=true&referer=&pver=v2&client=wdp";
                var root = rch.enable ? await rch.Get<JObject>($"{init.host}/{uri}", httpHeaders(init)) : await HttpClient.Get<JObject>($"{init.host}/{uri}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (root == null || !root.ContainsKey("video_balancer"))
                    return res.Fail("video_balancer");

                return root["video_balancer"].Value<string>("m3u8");
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return ContentTo(VideoTpl.ToJson("play", HostStreamProxy(init, cache.Value, proxy: proxy), "auto", vast: init.vast));
        }
    }
}
