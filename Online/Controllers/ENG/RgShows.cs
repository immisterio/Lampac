using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using System.Net;

namespace Online.Controllers
{
    public class RgShows : BaseENGController
    {
        [HttpGet]
        [Route("lite/rgshows")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Rgshows, checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, mp4: true, method: "call", hls_manifest_timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }


        #region Video
        [HttpGet]
        [Route("lite/rgshows/video")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Rgshows);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/main/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/main/tv/{id}/{s}/{e}";

            return await InvkSemaphore(init, embed, async () =>
            {
                string file = await magic(embed, init, proxyManager, proxy.proxy);
                if (file == null)
                    return StatusCode(502);

                file = HostStreamProxy(init, file, proxy: proxy.proxy);

                if (play)
                    return RedirectToPlay(file);

                return ContentTo(VideoTpl.ToJson("play", file, "English", vast: init.vast, headers: init.streamproxy ? null : httpHeaders(init.host, init.headers_stream), hls_manifest_timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds));
            });
        }
        #endregion

        #region magic
        async ValueTask<string> magic(string uri, OnlinesSettings init, ProxyManager proxyManager, WebProxy proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"rgshows:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string file))
                {
                    var root = await Http.Get<JObject>(uri, timeoutSeconds: 40, httpversion: 2, headers: httpHeaders(init));
                    if (root == null || !root.ContainsKey("stream"))
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    file = root["stream"].Value<string>("url");
                    if (string.IsNullOrEmpty(file))
                        return null;

                    proxyManager.Success();
                    hybridCache.Set(memKey, file, cacheTime(20, init: init));
                }

                return file;
            }
            catch { return null; }
        }
        #endregion
    }
}
