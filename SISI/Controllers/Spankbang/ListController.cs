using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;
using Shared.Engine.SISI;

namespace Lampac.Controllers.Spankbang
{
    public class ListController : BaseController
    {
        #region headers
        public static List<(string name, string val)> headers = new List<(string name, string val)>() 
        {
            ("cache-control", "no-cache"),
            ("dnt", "1"),
            ("pragma", "no-cache"),
            ("sec-ch-ua", "\"Chromium\";v=\"112\", \"Google Chrome\";v=\"112\", \"Not:A-Brand\";v=\"99\""),
            ("sec-ch-ua-mobile", "?0"),
            ("sec-ch-ua-platform", "\"Windows\""),
            ("sec-fetch-dest", "empty"),
            ("sec-fetch-mode", "cors"),
            ("sec-fetch-site", "cross-site"),
            ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/112.0.0.0 Safari/537.36")
        };
        #endregion

        [HttpGet]
        [Route("sbg")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.Spankbang.enable)
                return OnError("disable");

            string memKey = $"sbg:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                string html = await SpankbangTo.InvokeHtml(AppInit.conf.Spankbang.host, search, sort, pg, url => HttpClient.Get(url, httpversion: 2, timeoutSeconds: 10, useproxy: AppInit.conf.Spankbang.useproxy, addHeaders: headers));
                if (html == null)
                    return OnError("html");

                playlists = SpankbangTo.Playlist($"{host}/sbg/vidosik", html, pl =>
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
                menu = SpankbangTo.Menu(host, sort),
                list = playlists
            });
        }
    }
}
