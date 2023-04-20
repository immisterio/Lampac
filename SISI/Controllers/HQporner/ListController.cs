using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;

namespace Lampac.Controllers.HQporner
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("hqr")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.HQporner.enable)
                return OnError("disable");

            string memKey = $"hqr:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                string html = await HQpornerTo.InvokeHtml(AppInit.conf.HQporner.host, search, sort, pg, url => HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.HQporner.useproxy));
                if (html == null)
                    return OnError("html");

                playlists = HQpornerTo.Playlist($"{host}/hqr/vidosik", html, pl =>
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
                menu = HQpornerTo.Menu(host, sort),
                list = playlists
            });
        }
    }
}
