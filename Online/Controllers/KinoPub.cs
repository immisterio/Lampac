using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Engine.Online;
using System;
using Lampac.Models.LITE.KinoPub;
using Shared.Model.Online.KinoPub;

namespace Lampac.Controllers.LITE
{
    public class KinoPub : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("kinopub", AppInit.conf.KinoPub);

        #region kinopubpro
        [HttpGet]
        [Route("lite/kinopubpro")]
        async public Task<ActionResult> Pro(string code, string name)
        {
            var proxy = proxyManager.Get();
            var init = AppInit.conf.KinoPub;

            if (string.IsNullOrWhiteSpace(code))
            {
                var token_request = await HttpClient.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_code&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh", "", proxy: proxy);

                if (token_request == null)
                    return Content($"нет доступа к {init.corsHost()}", "text/html; charset=utf-8");

                string html = "1. Откройте <a href='https://kino.pub/device'>https://kino.pub/device</a> <br>";
                html += $"2. Введите код активации <b>{token_request.Value<string>("user_code")}</b><br>";
                html += $"3. Когда на сайте kino.pub появится \"Ожидание устройства\", нажмите кнопку \"Проверить активацию\" которая ниже</b>";

                html += $"<br><br><a href='/lite/kinopubpro?code={token_request.Value<string>("code")}&name={name}'><button>Проверить активацию</button></a>";

                return Content(html, "text/html; charset=utf-8");
            }
            else
            {
                var device_token = await HttpClient.Post<JObject>($"{init.corsHost()}/oauth2/device?grant_type=device_token&client_id=xbmc&client_secret=cgg3gtifu46urtfp2zp1nqtba0k2ezxh&code={code}", "", proxy: proxy);
                if (device_token == null || string.IsNullOrWhiteSpace(device_token.Value<string>("access_token")))
                    return LocalRedirect("/lite/kinopubpro");

                if (!string.IsNullOrEmpty(name))
                    await HttpClient.Post($"{init.corsHost()}/v1/device/notify?access_token={device_token.Value<string>("access_token")}", $"&title={name}", proxy: proxy);

                return Content("Добавьте в init.conf<br><br>\"KinoPub\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + device_token.Value<string>("access_token") + "\"<br>}", "text/html; charset=utf-8");
            }
        }
        #endregion

        [HttpGet]
        [Route("lite/kinopub")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, int year, int clarification, int postid, int s = -1)
        {
            var init = AppInit.conf.KinoPub;

            if (!init.enable)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxy = proxyManager.Get();

            string token = init.token;
            if (init.tokens != null && init.tokens.Length > 1)
                token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

            var oninvk = new KinoPubInvoke
            (
               host,
               init.corsHost(),
               token,
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               (stream, filepath) => HostStreamProxy(init, stream, proxy: proxy),
               requesterror: () => proxyManager.Refresh()
            );

            if (postid == 0)
            {
                var search = await InvokeCache<SearchResult>($"kinopub:search:{title}:{clarification}:{imdb_id}", cacheTime(40, init: init), proxyManager, async res =>
                {
                    if (rch.IsNotConnected())
                        return res.Fail(rch.connectionMsg);

                    return await oninvk.Search(title, original_title, year, clarification, imdb_id, kinopoisk_id);
                });

                if (!search.IsSuccess)
                    return OnError(search.ErrorMsg);

                if (search.Value.similars != null)
                    return Content(search.Value.similars, "text/html; charset=utf-8");

                postid = search.Value.id;
            }

            var cache = await InvokeCache<RootObject>($"kinopub:post:{postid}", cacheTime(10, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Post(postid);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, init.filetype, title, original_title, postid, s));
        }
    }
}
