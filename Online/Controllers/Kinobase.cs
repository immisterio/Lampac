using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.reCAPTCHA;
using Shared.Engine.Online;
using Online;
using Shared.Engine.CORE;
using Shared.Model.Online.Kinobase;

namespace Lampac.Controllers.LITE
{
    public class Kinobase : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("kinobase", AppInit.conf.Kinobase);

        [HttpGet]
        [Route("lite/kinobase")]
        async public Task<ActionResult> Index(string title, int year, int s = -1)
        {
            var init = AppInit.conf.Kinobase;

            if (!init.enable)
                return OnError();

            if (string.IsNullOrEmpty(title) || year == 0)
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxy = proxyManager.Get();

            var oninvk = new KinobaseInvoke
            (
               host,
               init.corsHost(),
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, referer: init.host, httpversion: 2, headers: httpHeaders(init)),
               (url, data) => init.rhub ? rch.Post(init.cors(url), data) : HttpClient.Post(init.cors(url), data, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"kinobase:view:{title}:{year}", proxyManager), cacheTime(20, init: init), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(title, year);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, title, year, s));
        }


        #region anticaptcha
        [HttpGet]
        [Route("lite/kinobase/check")]
        async public Task<ActionResult> Check()
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.anticaptchakey))
                return Content("anticaptchakey");

            var proxy = proxyManager.Get();

            string location = await HttpClient.GetLocation("https://kinobase.org/films", proxy: proxy);
            if (location == null || !location.Contains("/check"))
                return Content("location ok");

            reCAPTCHAv2 api = new reCAPTCHAv2
            {
                ClientKey = AppInit.conf.anticaptchakey,
                WebsiteUrl = new Uri($"{AppInit.conf.Kinobase.host}/check"),
                WebsiteKey = "6Ld5MCUTAAAAALXvmUFVUdqxSgy9a8Kf3zeVvGEJ"
            };

            if (!api.CreateTask())
                return Content("API v2 send failed. " + api.ErrorMessage);
            else if (!api.WaitForResult())
                return Content("Could not solve the captcha.");
            else
            {
                string result = null;
                string googleReCaptchaResponse = api.GetTaskSolution().GRecaptchaResponse;
                if (googleReCaptchaResponse != null)
                    result = await HttpClient.Post($"{AppInit.conf.Kinobase.host}/check?cm=re", $"g-recaptcha-response={googleReCaptchaResponse}&return_uri=%2F", timeoutSeconds: 8, proxy: proxy);

                return Content(result ?? "null");
            }
        }
        #endregion
    }
}
