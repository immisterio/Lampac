using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Online.Controllers
{
    public class RgShows : BaseENGController
    {
        public RgShows() : base(AppInit.conf.Rgshows) { }

        [HttpGet]
        [Route("lite/rgshows")]
        public Task<ActionResult> Index(bool checksearch, long id, long tmdb_id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(checksearch, id, tmdb_id, imdb_id, title, original_title, serial, s, rjson, mp4: true, method: "call", hls_manifest_timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds);
        }

        #region Video
        [HttpGet]
        [Route("lite/rgshows/video")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            if (await IsRequestBlocked(rch: false, rch_check: !play))
                return badInitMsg;

            string embed = $"{init.host}/main/movie/{id}";
            if (s > 0)
                embed = $"{init.host}/main/tv/{id}/{s}/{e}";

            return await InvkSemaphore(embed, async () =>
            {
                string file = await magic(embed);
                if (file == null)
                    return StatusCode(502);

                file = HostStreamProxy(file);

                if (play)
                    return RedirectToPlay(file);

                return ContentTo(VideoTpl.ToJson("play", file, "English", vast: init.vast, headers: init.streamproxy ? null : httpHeaders(init.host, init.headers_stream), hls_manifest_timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds));
            });
        }
        #endregion

        #region magic
        async ValueTask<string> magic(string uri)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"rgshows:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string file))
                {
                    var root = await Http.Get<JObject>(uri, proxy: proxy, timeoutSeconds: 40, httpversion: 2, headers: httpHeaders(init));
                    if (root == null || !root.ContainsKey("stream"))
                    {
                        proxyManager?.Refresh();
                        return null;
                    }

                    file = root["stream"].Value<string>("url");
                    if (string.IsNullOrEmpty(file))
                        return null;

                    proxyManager?.Success();
                    hybridCache.Set(memKey, file, cacheTime(20));
                }

                return file;
            }
            catch { return null; }
        }
        #endregion
    }
}
