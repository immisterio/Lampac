using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine.SISI;

namespace Lampac.Controllers.BongaCams
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("bgs")]
        async public Task<JsonResult> Index(string sort, int pg = 1)
        {
            if (!AppInit.conf.BongaCams.enable)
                return OnError("disable");

            string memKey = $"BongaCams:list:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                string html = await BongaCamsTo.InvokeHtml(AppInit.conf.BongaCams.host, sort, pg, url => 
                {
                    return HttpClient.Get(url, useproxy: AppInit.conf.BongaCams.useproxy, addHeaders: new List<(string name, string val)>()
                    {
                        ("dnt", "1"),
                        ("referer", AppInit.conf.BongaCams.host),
                        ("sec-fetch-dest", "empty"),
                        ("sec-fetch-mode", "cors"),
                        ("sec-fetch-site", "same-origin"),
                        ("x-requested-with", "XMLHttpRequest")
                    });
                });

                if (html == null)
                    return OnError("html");

                playlists = BongaCamsTo.Playlist(html, picture => HostImgProxy(0, AppInit.conf.sisi.heightPicture, picture));
                if (playlists.Count == 0)
                    return OnError("playlists");

                memoryCache.Set(memKey, playlists, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 5 : 1));
            }

            return new JsonResult(new
            {
                menu = BongaCamsTo.Menu(host, sort),
                list = playlists.Select(i => new 
                {
                    i.name,
                    i.picture,
                    video = HostStreamProxy(AppInit.conf.BongaCams.streamproxy, i.video),
                    i.json,
                    i.time,
                    i.quality
                })
            });
        }
    }
}
