using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using System;
using Microsoft.Extensions.Caching.Memory;

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

            #region Формируем страницу запроса и меню
            string url = $"{AppInit.conf.Eporner.host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"search/{HttpUtility.UrlEncode(search)}/";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(sort))
                    url += $"{sort}/";
            }

            if (pg > 1)
                url += $"{pg}/";
            #endregion

            string memKey = $"Eporner:list:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out string html))
            {
                html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Eporner.useproxy);
                if (html == null || !Regex.IsMatch(html, "<div class=\"mb( hdy)?\""))
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
                        playlist_url = $"{AppInit.Host(HttpContext)}/epr",
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
                                playlist_url = $"{AppInit.Host(HttpContext)}/epr"
                            },
                            new MenuItem()
                            {
                                title = "Топ просмотра",
                                playlist_url = $"{AppInit.Host(HttpContext)}/epr?sort=most-viewed"
                            },
                            new MenuItem()
                            {
                                title = "Топ рейтинга",
                                playlist_url = $"{AppInit.Host(HttpContext)}/epr?sort=top-rated"
                            },
                            new MenuItem()
                            {
                                title = "Длинные ролики",
                                playlist_url = $"{AppInit.Host(HttpContext)}/epr?sort=longest"
                            },
                            new MenuItem()
                            {
                                title = "Короткие ролики",
                                playlist_url = $"{AppInit.Host(HttpContext)}/epr?sort=shortest"
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

            foreach (string row in Regex.Split(html, "<div class=\"mb( hdy)?\"").Skip(1))
            {
                var g = new Regex("<p class=\"mbtit\"><a href=\"/([^\"]+)\">([^<]+)</a>", RegexOptions.IgnoreCase).Match(row).Groups;
                string quality = new Regex("<div class=\"mvhdico\"([^>]+)?><span>([^\"<]+)", RegexOptions.IgnoreCase).Match(row).Groups[2].Value;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string img = new Regex(" data-src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(img))
                        img = new Regex("<img src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                    string duration = new Regex("<span class=\"mbtim\"([^>]+)?>([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[2].Value.Trim();

                    playlists.Add(new PlaylistItem()
                    {
                        name = g[2].Value,
                        video = $"{AppInit.Host(HttpContext)}/epr/vidosik?goni={HttpUtility.UrlEncode(g[1].Value)}",
                        picture = AppInit.HostImgProxy(HttpContext, 0, AppInit.conf.sisi.heightPicture, img),
                        quality = quality,
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
