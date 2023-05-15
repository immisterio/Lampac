using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;
using Shared.Engine.CORE;

namespace Lampac.Controllers.Chaturbate
{
    public class ListController : BaseController
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
                {
                    proxyManager.Refresh();
                    return OnError("html");
                }

                playlists = ChaturbateTo.Playlist($"{host}/chu/potok", html, pl => 
                {
                    pl.picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture);
                    return pl;
                });

                if (playlists.Count == 0)
                {
                    proxyManager.Refresh();
                    return OnError("playlists");
                }

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 5 : 1));
            }

            return new JsonResult(new
            {
                menu = ChaturbateTo.Menu(host, sort),
                list = playlists
            });
        }
    }
}
