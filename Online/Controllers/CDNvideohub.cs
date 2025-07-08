using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Newtonsoft.Json.Linq;

namespace Lampac.Controllers.LITE
{
    public class CDNvideohub : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/cdnvideohub")]
        async public Task<ActionResult> Index(string title, string original_title, long kinopoisk_id, bool origsource = false, bool rjson = false)
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
                string json = rch.enable ? await rch.Get(uri, httpHeaders(init)) : await HttpClient.Get(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (string.IsNullOrEmpty(json))
                    return res.Fail("json");

                try
                {
                    var jsonData = JObject.Parse(json);
                    return jsonData;
                }
                catch
                {
                    return res.Fail("parse json");
                }
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () =>
            {
                var mtpl = new MovieTpl(title, original_title);

                var jsonData = cache.Value;
                var videos = jsonData["video"] as JArray;

                if (videos != null && videos.Count > 0)
                {
                    foreach (var video in videos)
                    {
                        var sources = video["sources"];
                        if (sources != null)
                        {
                            string hlsUrl = sources["hlsUrl"]?.ToString();
                            string voiceStudio = video["voice_studio"]?.ToString() ?? "По умолчанию";
                            string voiceType = video["voice_type"]?.ToString();

                            if (!string.IsNullOrEmpty(hlsUrl))
                            {
                                string quality = voiceStudio;
                                if (!string.IsNullOrEmpty(voiceType))
                                    quality += $" ({voiceType})";

                                mtpl.Append(quality, HostStreamProxy(init, hlsUrl.Replace("\\u0026", "&"), proxy: proxy), vast: init.vast);
                            }
                        }
                    }
                }

                return rjson ? mtpl.ToJson() : mtpl.ToHtml();

            }, origsource: origsource, gbcache: !rch.enable);
        }
    }
}
