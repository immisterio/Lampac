using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.PornHub
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("phub")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.PornHub.enable)
                return OnError("disable");

            #region Формируем страницу запроса
            string url = $"{AppInit.conf.PornHub.host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"video/search?search={HttpUtility.UrlEncode(search)}";
            }
            else
            {
                url += "video";

                if (!string.IsNullOrWhiteSpace(sort))
                    url += $"?o={sort}";
            }

            if (pg > 1)
                url += $"{(url.Contains("?") ? "&" : "?")}page={pg}";
            #endregion

            string memKey = $"PornHub:list:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out string html))
            {
                html = await HttpClient.Get(url);
                if (html == null || !html.Contains("PornHub - RSS Feed"))
                    return OnError("html");

                memoryCache.Set(memKey, html, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 15 : 5));
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
                        playlist_url = $"{AppInit.Host(HttpContext)}/phub",
                    },
                    new MenuItem()
                    {
                        title = $"Сортировка: {getSortName(sort, "Недавно в избранном")}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Недавно в избранном",
                                playlist_url = $"{AppInit.Host(HttpContext)}/phub"
                            },
                            new MenuItem()
                            {
                                title = "Новейшее",
                                playlist_url = $"{AppInit.Host(HttpContext)}/phub?sort=cm"
                            },
                            new MenuItem()
                            {
                                title = "Самые горячие",
                                playlist_url = $"{AppInit.Host(HttpContext)}/phub?sort=ht"
                            },
                            new MenuItem()
                            {
                                title = "Лучшие",
                                playlist_url = $"{AppInit.Host(HttpContext)}/phub?sort=tr"
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
            html = StringConvert.FindLastText(html, "id=\"videoCategory\"");

            foreach (string row in html.Split("pcVideoListItem "))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("premiumIcon"))
                    continue;

                string title = new Regex("<a href=\"/[^\"]+\" title=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                string vkey = new Regex("(-|_)vkey=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[2].Value;

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(vkey))
                {
                    string img = new Regex("data-src( +)?=( +)?\"(https?://[^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[3].Value;
                    string duration = new Regex("<var class=\"duration\">([^<]+)</var>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                    playlists.Add(new PlaylistItem()
                    {
                        name = title,
                        video = $"{AppInit.Host(HttpContext)}/phub/vidosik.m3u8?goni={vkey}",
                        picture = $"{AppInit.Host(HttpContext)}/proxyimg:0:{AppInit.conf.SisiHeightPicture}/{img}",
                        time = duration
                    });
                }
            }

            return playlists;
        }
        #endregion

        #region getSortName
        string getSortName(string sort, string emptyName)
        {
            if (string.IsNullOrWhiteSpace(sort))
                return emptyName;

            switch (sort)
            {
                case "mr":
                case "cm":
                    return "новейшее";

                case "ht":
                    return "самые горячие";

                case "vi":
                case "mv":
                    return "больше просмотров";

                case "ra":
                case "tr":
                    return "лучшие";

                default:
                    return emptyName;
            }
        }
        #endregion
    }
}
