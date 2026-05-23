using Filmix.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Attributes;
using System.Net.Http;

namespace Filmix;

public class Filmix : BaseOnlineController<FilmixSettings>
{
    static readonly HttpClient httpClient = FriendlyHttp.CreateHttpClient();

    public Filmix() : base(ModInit.conf.Filmix)
    {
        requestInitialization += () =>
        {
            if (init.httpversion == 1)
                httpHydra.RegisterHttp(httpClient);
        };

        loadKitInitialization = (j, i, c) =>
        {
            if (j.ContainsKey("reserve"))
                i.reserve = c.reserve;

            i.pro = c.pro;
            i.tokens = c.tokens;
            return i;
        };
    }

    #region filmixpro
    [HttpGet]
    [AllowAnonymous]
    [Route("lite/filmixpro")]
    async public Task<ActionResult> Pro()
    {
        if (!requestInfo.IsLocalIp)
            return ContentTo("is not local ip");

        string uri = $"{init.host}/api/v2/token_request?user_dev_apk=2.2.13&user_dev_id={UnicTo.Code(16)}&user_dev_name=Xiaomi&user_dev_os=12&user_dev_vendor=Xiaomi&user_dev_token=";
        var token_request = await Http.Get<JObject>(uri, httpversion: init.httpversion, proxy: proxy);

        if (token_request == null)
            return ContentTo($"нет доступа к {init.host}");

        string html = "1. Откройте <a href='https://filmix.my/consoles'>https://filmix.my/consoles</a> <br>";
        html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
        html += $"<br><br><br>Добавьте в init.conf<br><br>";
        html += "\"Filmix\": {<br>&nbsp;&nbsp;\"token\": \"" + token_request.Value<string>("code") + "\",<br>&nbsp;&nbsp;\"pro\": true<br>}";

        return ContentTo(html);
    }
    #endregion

    [HttpGet, Staticache(manually: true)]
    [Route("lite/filmix")]
    async public Task<ActionResult> Index(string title, string original_title, byte clarification, short year, int postid, short t, short? s = null, bool rjson = false, bool similar = false, string source = null, string id = null)
    {
        if (await IsRequestBlocked(rch: true))
            return badInitMsg;

        if (postid == 0 && !string.IsNullOrEmpty(source) && !string.IsNullOrEmpty(id))
        {
            if (source.Equals("filmix", StringComparison.OrdinalIgnoreCase) || source.Equals("filmixapp", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(id, out postid))
                    int.TryParse(Regex.Match(id, "/([0-9]+)-").Groups[1].Value, out postid);
            }
        }

        string token = init.token;
        if (init.tokens != null && init.tokens.Length > 1)
            token = init.tokens[Random.Shared.Next(0, init.tokens.Length)];

        var oninvk = new FilmixInvoke
        (
           init,
           host,
           token,
           "lite/filmix",
           httpHydra,
           streamfile => HostStreamProxy(streamfile),
           rjson: rjson
        );

        if (postid == 0)
        {
            var search = await InvokeCacheResult($"filmix:search:{title}:{original_title}:{year}:{clarification}:{similar}", 40,
                () => oninvk.Search(title, original_title, clarification, year, similar)
            );

            if (!search.IsSuccess)
                return OnError(search.ErrorMsg);

            if (search.Value.id == 0)
                return ContentTpl(search.Value.similars);

            postid = search.Value.id;
        }

    rhubFallback:
        var cache = await InvokeCacheResult($"filmix:post:{postid}:{token}", 20,
            () => oninvk.Post(postid)
        );

        if (IsRhubFallback(cache, safety: !string.IsNullOrEmpty(token)))
            goto rhubFallback;

        return ContentTpl(cache,
            () => oninvk.Tpl(cache.Value, init.pro, postid, title, original_title, t, s, vast: init.vast)
        );
    }
}
