using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.Xvideos
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("xds")]
        async public Task<JsonResult> Index(string search, int pg = 1)
        {
            if (!AppInit.conf.Xvideos.enable)
                return OnError("disable");

            string url = $"{AppInit.conf.Xvideos.host}/new/{pg}";
            if (!string.IsNullOrWhiteSpace(search))
                url = $"{AppInit.conf.Xvideos.host}/?k={HttpUtility.UrlEncode(search)}&p={pg}";

            string memKey = $"Xvideos:list:{search}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out string html))
            {
                html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Xvideos.useproxy);
                if (html == null || !html.Contains("<div class=\"thumb-inside\">"))
                    return OnError("html");

                memoryCache.Set(memKey, html, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            var playlists = new List<PlaylistItem>();
            foreach (string row in Regex.Split(html, "<div class=\"thumb-inside\">").Skip(1))
            {
                var g = new Regex($"<a href=\"/(prof-video-click/[^\"]+|video[0-9]+/[^\"]+)\" title=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                string qmark = new Regex("<span class=\"video-hd-mark\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("<span class=\"duration\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();

                    string img = new Regex("data-src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    img = Regex.Replace(img, "/videos/thumbs([0-9]+)/", "/videos/thumbs$1lll/");
                    img = Regex.Replace(img, "\\.THUMBNUM\\.(jpg|png)$", ".1.$1", RegexOptions.IgnoreCase);

                    playlists.Add(new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{AppInit.Host(HttpContext)}/xds/vidosik?goni={HttpUtility.UrlEncode(g[1].Value)}",
                        picture = AppInit.HostImgProxy(HttpContext, 0, AppInit.conf.sisi.heightPicture, img),
                        quality = string.IsNullOrWhiteSpace(qmark) ? null : qmark,
                        time = duration,
                        json = true
                    });
                }
            }

            if (playlists.Count == 0)
                return OnError("playlists");

            return new JsonResult(new
            {
                menu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = "Поиск",
                        search_on = "search_on",
                        playlist_url = $"{AppInit.Host(HttpContext)}/xds",
                    }
                },
                list = playlists
            });
        }
    }
}
