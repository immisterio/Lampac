using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Chaturbate
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("chu")]
        async public Task<JsonResult> Index(string sort, int pg = 1)
        {
            if (!AppInit.conf.Chaturbate.enable)
                return OnError("disable");

            string memKey = $"Chaturbate:list:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("chu", AppInit.conf.Chaturbate);
                var proxy = proxyManager.Get();

                string html = await ChaturbateTo.InvokeHtml(AppInit.conf.Chaturbate.corsHost(), sort, pg, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager);

                playlists = ChaturbateTo.Playlist($"{host}/chu/potok", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 5 : 1));
            }

            return OnResult(playlists, ChaturbateTo.Menu(host, sort));
        }
    }
}
