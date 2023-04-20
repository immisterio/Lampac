using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Xnxx
{
    public class ListController : BaseController
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
                string html = await XnxxTo.InvokeHtml(AppInit.conf.Xnxx.host, search, pg, url => HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Xnxx.useproxy));
                if (html == null)
                    return OnError("html");

                playlists = XnxxTo.Playlist($"{host}/xnx/vidosik", html, pl =>
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
                menu = XnxxTo.Menu(host),
                list = playlists
            });
        }
    }
}
