using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using System;
using Shared.Model.Online.Filmix;
using Shared.Model.Online;
using System.Text;
using Shared.Model.Online.FilmixTV;

namespace Lampac.Controllers.LITE
{
    /// <summary>
    /// Автор https://github.com/fellicienne
    /// https://github.com/immisterio/Lampac/pull/41
    /// </summary>
    public class FilmixTV : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/filmixtv")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t, int? s = null, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.FilmixTV.Clone();

            if (!init.enable || string.IsNullOrEmpty(init.user_apitv))
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            var proxyManager = new ProxyManager("filmixtv", init);
            var proxy = proxyManager.Get();

            #region accessToken
            if (string.IsNullOrEmpty(init.token_apitv))
            {
                var auth = await InvokeCache<string>("filmixtv:accessToken", TimeSpan.FromHours(8), proxyManager, async res =>
                {
                    string url_api = $"{init.corsHost()}/api-fx/auth";
                    string data = $"{{\"user_name\":\"{init.user_apitv}\",\"user_passw\":\"{init.passwd_apitv}\"}}";

                    var jobject = await HttpClient.Post<JObject>(url_api, new System.Net.Http.StringContent(data, Encoding.UTF8, "application/json"), timeoutSeconds: 8);

                    return jobject?.GetValue("accessToken")?.ToString();
                });

                if (!auth.IsSuccess)
                    return OnError(auth.ErrorMsg);

                if (string.IsNullOrEmpty(auth.Value))
                    return OnError();

                init.token_apitv = auth.Value;
            }
            #endregion

            var oninvk = new FilmixTVInvoke
            (
               host,
               init.corsHost(),
               ongettourl => HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (url, data, head) => HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, headers: httpHeaders(init, head)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => proxyManager.Refresh(),
               rjson: rjson
            );

            if (postid == 0)
            {
                var search = await InvokeCache<SearchResult>($"filmixtv:search:{title}:{original_title}:{clarification}", cacheTime(40, init: init), proxyManager, async res =>
                {
                    return await oninvk.Search(title, original_title, clarification, year);
                });

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.id == 0)
                    return ContentTo(rjson ? search.Value.similars.ToJson() : search.Value.similars.ToHtml());

                postid = search.Value.id;
            }

            var cache = await InvokeCache<RootObject>($"filmixtv:post:{postid}", cacheTime(20, init: init), proxyManager, inmemory: true, onget: async res =>
            {
                string json = await HttpClient.Get($"{init.corsHost()}/api-fx/post/{postid}/video-links", timeoutSeconds: 8, headers: HeadersModel.Init("Authorization", $"Bearer {init.token_apitv}"));

                return oninvk.Post(json);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, init.pro, postid, title, original_title, t, s), origsource: origsource);
        }
    }
}
