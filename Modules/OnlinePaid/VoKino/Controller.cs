using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Services;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace VoKino;

public class VoKino : BaseOnlineController<ModuleConf>
{
    static readonly HttpClient httpClient = FriendlyHttp.CreateHttpClient();

    public VoKino() : base(ModInit.conf)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 1)
                httpHydra.RegisterHttp(httpClient);
        };

        loadKitInitialization = (j, i, c) =>
        {
            if (j.ContainsKey("online"))
                i.online = c.online;

            return i;
        };
    }

    #region vokinotk
    [HttpGet]
    [AllowAnonymous]
    [Route("lite/vokinotk")]
    async public Task<ActionResult> Token(string login, string pass)
    {
        string html = string.Empty;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(pass))
        {
            html = "Введите данные аккаунта <a href='https://vokino.pro'>vokino.pro</a> <br> <br><form method=\"get\" action=\"/lite/vokinotk\"><input type=\"text\" name=\"login\" placeholder=\"email\"> &nbsp; &nbsp; <input type=\"text\" name=\"pass\" placeholder=\"пароль\"><br><br><button>Добавить устройство</button></form> ";
        }
        else
        {
            string deviceid = new string(DateTime.Now.ToBinary().ToString().Reverse().ToArray()).Substring(0, 8);
            string uri = $"{init.host}/v2/auth?email={HttpUtility.UrlEncode(login)}&passwd={HttpUtility.UrlEncode(pass)}&deviceid={deviceid}";

            var head = HeadersModel.Init(
                ("user-agent", "lampac")
            );

            var token_request = await Http.Get<JObject>(uri, proxy: proxy, headers: head);

            if (token_request == null)
                return ContentTo($"нет доступа к {init.host}");

            string authToken = token_request.Value<string>("authToken");
            if (string.IsNullOrEmpty(authToken))
                return ContentTo(token_request.Value<string>("error") ?? "Не удалось получить токен");

            html = "Добавьте в init.conf<br><br>\"VoKino\": {<br>&nbsp;&nbsp;\"enable\": true,<br>&nbsp;&nbsp;\"token\": \"" + authToken + "\"<br>}";
        }

        return ContentTo(html);
    }
    #endregion

    [HttpGet, Staticache(manually: true)]
    [Route("lite/vokino")]
    async public Task<ActionResult> Index(bool checksearch, string origid, long kinopoisk_id, string title, string original_title, string balancer, string t, short s = -1, bool rjson = false, string source = null, string id = null)
    {
        if (string.IsNullOrEmpty(origid) && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("vokino", StringComparison.OrdinalIgnoreCase))
                origid = id;
        }

        if (kinopoisk_id == 0 && string.IsNullOrEmpty(origid))
            return OnError();

        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (string.IsNullOrEmpty(init.token))
            return OnError("token", statusCode: 401, gbcache: false);

        if (balancer is "filmix" or "monframe")
            init.streamproxy = false;

        if (checksearch /*&& balancer != "vokino"*/)
            return Content("data-json="); // заглушка от 429 и от +1 к просмотру

        var oninvk = new VoKinoInvoke
        (
           host,
           init.host,
           init.token,
           httpHydra,
           streamfile => HostStreamProxy(streamfile)
        );

    rhubFallback:
        var cache = await InvokeCacheResult(ipkey($"vokino:{kinopoisk_id}:{origid}:{balancer}:{t}:{init.token}"), 20,
            () => oninvk.Embed(origid, kinopoisk_id, balancer, t),
            textJson: true
        );

        if (IsRhubFallback(cache, safety: true))
            goto rhubFallback;

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, origid, kinopoisk_id, title, original_title, balancer, t, s, init.vast, rjson)
        );
    }
}
