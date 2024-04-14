using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Linq;
using Online;
using Shared.Engine.CORE;
using Shared.Engine.Online;
using Shared.Model.Online.VoKino;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class VoKino : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("vokino", AppInit.conf.VoKino);

        #region vokinotk
        [HttpGet]
        [Route("lite/vokinotk")]
        async public Task<ActionResult> Token(string login, string pass)
        {
            string html = string.Empty;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
            {
                html = "Введите данные аккаунта <a href='http://vokino.tv'>vokino.tv</a> <br> <br><form method=\"get\" action=\"/lite/vokinotk\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Добавить устройство</button></form> ";
            }
            else
            {
                string deviceid = new string(DateTime.Now.ToBinary().ToString().Reverse().ToArray()).Substring(0, 8);
                var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.VoKino.corsHost()}/v2/auth?email={HttpUtility.UrlEncode(login)}&passwd={HttpUtility.UrlEncode(pass)}&deviceid={deviceid}", proxy: proxyManager.Get());

                if (token_request == null)
                    return Content($"нет доступа к {AppInit.conf.VoKino.corsHost()}", "text/html; charset=utf-8");

                html = "Добавьте в init.conf<br><br>\"VoKino\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + token_request.Value<string>("authToken") + "\"<br>}";
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/vokino")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int s = -1)
        {
            var init = AppInit.conf.VoKino;

            if (!init.enable || kinopoisk_id == 0 || string.IsNullOrEmpty(init.token))
                return OnError();

            var rch = new RchClient(HttpContext, host, init.rhub);
            var proxy = proxyManager.Get();

            var oninvk = new VoKinoInvoke
            (
               host,
               init.corsHost(),
               init.token,
               ongettourl => init.rhub ? rch.Get(init.cors(ongettourl)) : HttpClient.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => proxyManager.Refresh()
            );

            var cache = await InvokeCache<List<Сhannel>>(rch.ipkey($"vokino:{kinopoisk_id}", proxyManager), cacheTime(20), proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(kinopoisk_id);
            });

            return OnResult(cache, () => oninvk.Html(cache.Value, kinopoisk_id, title, original_title, s));
        }
    }
}
