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

namespace Lampac.Controllers.Porntrex
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("ptx")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.Porntrex.enable)
                return OnError("disable");

            #region Формируем страницу запроса
            string url = $"{AppInit.conf.Porntrex.host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{AppInit.conf.Porntrex.host}/search/{HttpUtility.UrlEncode(search)}/latest-updates/?from_videos={pg}";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(sort))
                {
                    url += $"latest-updates/{pg}/";
                }
                else
                {
                    url += $"{sort}/weekly/?from4={pg}";
                }
            }
            #endregion

            string memKey = $"Porntrex:list:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out string html))
            {
                html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Porntrex.useproxy);
                if (html == null || !html.Contains("<div class=\"video-preview-screen"))
                    return OnError("html");

                memoryCache.Set(memKey, html, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            var playlists = getTubes(html);
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
                        playlist_url = $"{AppInit.Host(HttpContext)}/ptx",
                    },
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новинки" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Новинки",
                                playlist_url = $"{AppInit.Host(HttpContext)}/ptx"
                            },
                            new MenuItem()
                            {
                                title = "Топ просмотров",
                                playlist_url = $"{AppInit.Host(HttpContext)}/ptx?sort=most-popular"
                            }
                        }
                    }
                },
                list = playlists
            });
        }


        #region getTubes
        List<PlaylistItem> getTubes(string html)
        {
            var playlists = new List<PlaylistItem>();

            foreach (string row in Regex.Split(html, "<div class=\"video-preview-screen").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("<span class=\"line-private\">"))
                    continue;

                var g = new Regex($"<a href=\"{AppInit.conf.Porntrex.host}/(video/[^\"]+)\" title=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                string quality = new Regex("<span class=\"quality\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("<i class=\"fa fa-clock-o\"></i>([^<]+)</div>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    var img = new Regex("data-src=\"(https?:)?//((statics.cdntrex.com/contents/videos_screenshots/[0-9]+/[0-9]+)[^\"]+)", RegexOptions.IgnoreCase).Match(row).Groups;

                    playlists.Add(new PlaylistItem()
                    {
                        video = $"{AppInit.Host(HttpContext)}/ptx/vidosik?goni={HttpUtility.UrlEncode(g[1].Value)}",
                        name = g[2].Value,
                        picture = $"{AppInit.Host(HttpContext)}/proxyimg/https://{img[2].Value}",
                        quality = !string.IsNullOrEmpty(quality) ? quality : null,
                        time = duration,
                        json = true
                    });
                }
            }

            return playlists;
        }
        #endregion
    }
}
