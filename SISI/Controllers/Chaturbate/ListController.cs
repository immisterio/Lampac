using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;

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
                string html = await ChaturbateTo.InvokeHtml(AppInit.conf.Chaturbate.corsHost(), sort, pg, url => HttpClient.Get(url, useproxy: AppInit.conf.Chaturbate.useproxy));
                if (html == null)
                    return OnError("html");

                playlists = ChaturbateTo.Playlist($"{host}/chu/potok", html, picture => HostImgProxy(0, AppInit.conf.sisi.heightPicture, picture));
                if (playlists.Count == 0)
                    return OnError("playlists");

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
