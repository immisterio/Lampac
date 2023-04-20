using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Eporner
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("epr")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.Eporner.enable)
                return OnError("disable");

            pg += 1;
            string memKey = $"epr:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                string html = await EpornerTo.InvokeHtml(AppInit.conf.Eporner.host, search, sort, pg, url => HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Eporner.useproxy));
                if (html == null)
                    return OnError("html");

                playlists = EpornerTo.Playlist($"{host}/epr/vidosik", html, pl =>
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
                menu = EpornerTo.Menu(host, sort),
                list = playlists
            });
        }
    }
}
