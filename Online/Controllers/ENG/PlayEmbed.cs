using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Lampac.Models.LITE;
using Newtonsoft.Json.Linq;
using Lampac.Engine.CORE;
using System.Web;
using System.Net;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class PlayEmbed : BaseENGController
    {
        [HttpGet]
        [Route("lite/playembed")]
        public Task<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            return ViewTmdb(AppInit.conf.Playembed, false, checksearch, id, imdb_id, title, original_title, serial, s, rjson);
        }


        #region Video
        [HttpGet]
        [Route("lite/playembed/video.m3u8")]
        async public Task<ActionResult> Video(string imdb_id, int s = -1, int e = -1)
        {
            var init = await loadKit(AppInit.conf.Playembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (string.IsNullOrEmpty(imdb_id))
                return OnError();

            string m3u = null;
            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            for (int i = 3; i > 0; i--)
            {
                string embed = $"{init.host}/server/{i}?path=/movie/{imdb_id}";
                if (s > 0)
                    embed = $"{init.host}/server/{i}?path=/tv/{imdb_id}/{s}/{e}";

                m3u = await magic(embed, init, proxy.proxy);
                if (!string.IsNullOrEmpty(m3u))
                    break;
            }

            if (string.IsNullOrEmpty(m3u))
                return StatusCode(502);

            return Redirect(HostStreamProxy(init, m3u, proxy: proxy.proxy));
        }
        #endregion

        #region magic
        async ValueTask<string> magic(string uri, OnlinesSettings init, WebProxy proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"playembed:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string m3u8))
                {
                    var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 30, headers: httpHeaders(init));
                    if (root == null || !root.ContainsKey("playlist"))
                        return null;

                    m3u8 = root["playlist"].First.Value<string>("file");
                    m3u8 = Regex.Match(m3u8, "url=([^&]+)").Groups[1].Value;
                    if (string.IsNullOrEmpty(m3u8))
                        return null;

                    m3u8 = HttpUtility.UrlDecode(m3u8);
                    hybridCache.Set(memKey, m3u8, cacheTime(20, init: init));
                }

                return m3u8;
            }
            catch { return null; }
        }
        #endregion
    }
}
