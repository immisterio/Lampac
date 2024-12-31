﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Chaturbate
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("chu")]
        async public Task<ActionResult> Index(string search, string sort, int pg = 1)
        {
            var init = AppInit.conf.Chaturbate.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            string memKey = $"Chaturbate:list:{sort}:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("chu", init);
                var proxy = proxyManager.Get();

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error, false);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string html = await ChaturbateTo.InvokeHtml(init.corsHost(), sort, pg, url =>
                    rch.enable ? rch.Get(init.cors(url), httpHeaders(init)) : HttpClient.Get(init.cors(url), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init))
                );

                playlists = ChaturbateTo.Playlist($"{host}/chu/potok", html);

                if (playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, !rch.enable);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(5, init: init));
            }

            return OnResult(playlists, ChaturbateTo.Menu(host, sort), plugin: "chu");
        }
    }
}
