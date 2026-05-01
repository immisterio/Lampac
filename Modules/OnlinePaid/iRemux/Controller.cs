using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Attributes;
using Shared.Models.Base;
using Shared.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Shared;

namespace iRemux;

public class iRemuxController : BaseOnlineController
{
    static ConcurrentDictionary<string, string> authCookie = new();

    iRemuxInvoke oninvk;

    public iRemuxController() : base(ModInit.conf)
    {
        requestInitializationAsync = async () =>
        {
            oninvk = new iRemuxInvoke
            (
               host,
               init.host,
               httpHydra,
               streamfile => HostStreamProxy(streamfile),
               cookie: await getCookie(init, memoryCache)
            );
        };
    }

    [HttpGet]
    [Staticache]
    [Route("lite/remux")]
    async public Task<ActionResult> Index(string title, string original_title, int year, string href, bool rjson = false)
    {
        if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
            return OnError();

        if (await IsRequestBlocked(rch: false))
            return badInitMsg;

        string mkey = string.IsNullOrEmpty(href) ? $"{title}:{original_title}:{year}" : href;

        var content = await InvokeCache($"remux:{mkey}", TimeSpan.FromDays(1),
            () => oninvk.Embed(title, original_title, year, href),
            textJson: true
        );

        if (content == null)
            return OnError();

        return ContentTpl(oninvk.Tpl(content, title, original_title, year));
    }


    [HttpGet]
    [Staticache]
    [Route("lite/remux/movie")]
    async public Task<ActionResult> Movie(string linkid, string quality, string title, string original_title)
    {
        if (await IsRequestBlocked(rch: false))
            return badInitMsg;

        string weblink = await InvokeCache($"remux:view:{linkid}:{proxyManager?.CurrentProxyIp}", 20, () => oninvk.Weblink(linkid));
        if (weblink == null)
            return OnError();

        return ContentTo(oninvk.Movie(weblink, quality, title, original_title, HttpContext, vast: init.vast));
    }


    #region getCookie
    async static ValueTask<string> getCookie(BaseSettings init, IMemoryCache memoryCache)
    {
        if (!string.IsNullOrWhiteSpace(init.cookie))
            return init.cookie.Trim();

        if (string.IsNullOrWhiteSpace(init.login) || string.IsNullOrWhiteSpace(init.passwd))
            return null;

        string keyCookie = $"{init.host}:{init.login}:{init.passwd}";

        if (authCookie.TryGetValue(keyCookie, out string _cook))
            return _cook;

        if (memoryCache.TryGetValue($"iremux:login:{init.login}", out _))
            return null;

        memoryCache.Set($"iremux:login:{init.login}", 0, TimeSpan.FromMinutes(2));

        try
        {
            using var clientHandler = new HttpClientHandler() { AllowAutoRedirect = false };
            clientHandler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

            using var client = new HttpClient(clientHandler) { Timeout = TimeSpan.FromSeconds(20) };

            foreach (var h in Http.defaultFullHeaders)
                client.DefaultRequestHeaders.TryAddWithoutValidation(h.Key, h.Value);

            var postParams = new Dictionary<string, string>
            {
                { "login_name", init.login },
                { "login_password", init.passwd },
                { "login", "submit" }
            };

            using var postContent = new FormUrlEncodedContent(postParams);
            using var response = await client.PostAsync($"{init.host}/", postContent);

            if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
                return null;

            string phpsessid = null;
            string dleUserId = null;
            string dlePassword = null;

            foreach (string line in cookies)
            {
                if (string.IsNullOrWhiteSpace(line) || line.Contains("=deleted;"))
                    continue;

                string cook = line.Split(';')[0];
                if (!cook.Contains('='))
                    continue;

                var part = cook.Split('=', 2);
                switch (part[0])
                {
                    case "PHPSESSID":
                        phpsessid = part[1];
                        break;
                    case "dle_user_id":
                        dleUserId = part[1];
                        break;
                    case "dle_password":
                        dlePassword = part[1];
                        break;
                }
            }

            if (string.IsNullOrEmpty(phpsessid) || string.IsNullOrEmpty(dleUserId) || string.IsNullOrEmpty(dlePassword))
                return null;

            string resultCookie = $"PHPSESSID={phpsessid}; dle_user_id={dleUserId}; dle_password={dlePassword}";
            authCookie[keyCookie] = resultCookie;

            return resultCookie;
        }
        catch
        {
            return null;
        }
    }
    #endregion
}
