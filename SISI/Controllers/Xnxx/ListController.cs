using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;
using Shared.Engine.CORE;
using SISI;

namespace Lampac.Controllers.Xnxx
{
    public class ListController : BaseSisiController
    {
        [HttpGet]
        [Route("xnx")]
        async public Task<JsonResult> Index(string search, int pg = 1)
        {
            if (!AppInit.conf.Xnxx.enable)
                return OnError("disable");

            string memKey = $"xnx:list:{search}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("xnx", AppInit.conf.Xnxx);
                var proxy = proxyManager.Get();

                string html = await XnxxTo.InvokeHtml(AppInit.conf.Xnxx.host, search, pg, url => HttpClient.Get(url, timeoutSeconds: 10, proxy: proxy));
                if (html == null)
                    return OnError("html", proxyManager, string.IsNullOrEmpty(search));

                playlists = XnxxTo.Playlist($"{host}/xnx/vidosik.m3u8", html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager, string.IsNullOrEmpty(search));

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return OnResult(playlists, string.IsNullOrEmpty(search) ? XnxxTo.Menu(host) : null);
        }
    }
}
