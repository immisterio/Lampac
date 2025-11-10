using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.VoKino;

namespace Online.Controllers
{
    public class VoKino : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.VoKino);

        #region vokinotk
        [HttpGet]
        [AllowAnonymous]
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
                var token_request = await Http.Get<JObject>($"{AppInit.conf.VoKino.corsHost()}/v2/auth?email={HttpUtility.UrlEncode(login)}&passwd={HttpUtility.UrlEncode(pass)}&deviceid={deviceid}", proxy: proxyManager.Get(), headers: HeadersModel.Init("user-agent", "lampac"));

                if (token_request == null)
                    return Content($"нет доступа к {AppInit.conf.VoKino.corsHost()}", "text/html; charset=utf-8");

                string authToken = token_request.Value<string>("authToken");
                if (string.IsNullOrEmpty(authToken))
                    return Content(token_request.Value<string>("error") ?? "Не удалось получить токен", "text/html; charset=utf-8");

                html = "Добавьте в init.conf<br><br>\"VoKino\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + authToken + "\"<br>}";
            }

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/vokino")]
        async public ValueTask<ActionResult> Index(bool checksearch, string origid, long kinopoisk_id, string title, string original_title, string balancer, string t, int s = -1, bool rjson = false, string source = null, string id = null)
        {
            if (string.IsNullOrEmpty(origid) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
            {
                if (source.ToLower() == "vokino")
                    origid = id;
            }

            var init = await loadKit(AppInit.conf.VoKino, (j, i, c) => 
            {
                if (j.ContainsKey("online"))
                    i.online = c.online;
                return i; 
            });

            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (string.IsNullOrEmpty(init.token))
                return OnError();

            if (kinopoisk_id == 0 && string.IsNullOrEmpty(origid))
                return OnError();

            if (balancer is "filmix" or "ashdi" or "monframe")
                init.streamproxy = false;

            if (checksearch /*&& balancer != "vokino"*/)
                return Content("data-json="); // заглушка от 429 и +1 к просмотру

            var proxy = proxyManager.Get();
            var rch = new RchClient(HttpContext, host, init, requestInfo);

            var oninvk = new VoKinoInvoke
            (
               host,
               init.corsHost(),
               init.token,
               ongettourl => rch.enable ? rch.Get(init.cors(ongettourl), httpHeaders(init)) : Http.Get(init.cors(ongettourl), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy),
               requesterror: () => { if (!rch.enable) { proxyManager.Refresh(); } }
            );

            reset:
            var cache = await InvokeCache<EmbedModel>(rch.ipkey($"vokino:{kinopoisk_id}:{origid}:{balancer}:{t}:{init.token}", proxyManager), cacheTime(20, rhub: 2, init: init), rch.enable ? null : proxyManager, async res =>
            {
                if (rch.IsNotConnected())
                    return res.Fail(rch.connectionMsg);

                return await oninvk.Embed(origid, kinopoisk_id, balancer, t);
            });

            if (IsRhubFallback(cache, init))
                goto reset;

            return OnResult(cache, () => oninvk.Html(cache.Value, origid, kinopoisk_id, title, original_title, balancer, t, s, init.vast, rjson), gbcache: !rch.enable);
        }
    }
}
