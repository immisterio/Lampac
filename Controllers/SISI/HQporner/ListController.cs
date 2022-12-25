using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using System.Linq;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;

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

            #region Формируем страницу запроса
            string url = $"{AppInit.conf.HQporner.host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"?q={HttpUtility.UrlEncode(search)}&p={pg}";
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(sort))
                    url += $"top/{sort}";

                else
                    url += "hdporn";

                if (pg > 1)
                    url += $"/{pg}";
            }
            #endregion

            string memKey = $"HQporner:list:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out string html))
            {
                html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.HQporner.useproxy);
                if (html == null || !html.Contains("<div class=\"6u\">"))
                    return OnError("html");

                memoryCache.Set(memKey, html, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            var playlists = new List<PlaylistItem>();
            foreach (string row in html.Split("<div class=\"6u\">").Skip(1))
            {
                var g = new Regex("href=\"/([^\"]+)\" class=\"click-trigger\">([^<]+)</a><", RegexOptions.IgnoreCase).Match(row).Groups;
                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[2].Value))
                {
                    string duration = new Regex("class=\"icon fa-clock-o meta-data\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    string img = new Regex("defaultImage\\(\"//([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(img))
                        img = "https://" + img;

                    playlists.Add(new PlaylistItem()
                    {
                        name = g[2].Value.Trim(),
                        video = $"{AppInit.Host(HttpContext)}/hqr/vidosik?goni={HttpUtility.UrlEncode(g[1].Value)}",
                        picture = AppInit.HostImgProxy(HttpContext, 0, AppInit.conf.sisi.heightPicture, img),
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
                        playlist_url = $"{AppInit.Host(HttpContext)}/hqr",
                    },
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новинки" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Самые новые",
                                playlist_url = $"{AppInit.Host(HttpContext)}/hqr"
                            },
                            new MenuItem()
                            {
                                title = "Топ недели",
                                playlist_url = $"{AppInit.Host(HttpContext)}/hqr?sort=week"
                            },
                            new MenuItem()
                            {
                                title = "Топ месяца",
                                playlist_url = $"{AppInit.Host(HttpContext)}/hqr?sort=month"
                            }
                        }
                    }
                },
                list = playlists
            });
        }
    }
}
