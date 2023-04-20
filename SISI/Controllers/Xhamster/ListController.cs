using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Xhamster
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("xmr")]
        async public Task<JsonResult> Index(string search, string sort = "newest", int pg = 1)
        {
            if (!AppInit.conf.Xhamster.enable)
                return OnError("disable");

            pg++;
            string memKey = $"xmr:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                string html = await XhamsterTo.InvokeHtml(AppInit.conf.Xhamster.host, search, sort, pg, url => HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Xhamster.useproxy));
                if (html == null)
                    return OnError("html");

                playlists = XhamsterTo.Playlist($"{host}/xmr/vidosik", html, pl =>
                {
                    pl.picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture);
                    return pl;
                });

                if (playlists.Count == 0)
                    return OnError("playlists");

                memoryCache.Set(memKey, playlists, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 10 : 2));
            }

            return new JsonResult(new
            {
                menu = XhamsterTo.Menu(host, sort),
                list = playlists
            });
        }
    }
}
