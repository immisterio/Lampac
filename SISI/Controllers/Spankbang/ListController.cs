using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.Spankbang
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("sbg")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.Spankbang.enable)
                return OnError("disable");

            #region Страница запроса
            string url = $"{AppInit.conf.Spankbang.host}/";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"s/{HttpUtility.UrlEncode(search)}/{pg}/";
            }
            else
            {
                url += $"{sort ?? "new_videos"}/{pg}/";

                if (sort == "most_popular")
                    url += "?p=m";
            }
            #endregion

            string memKey = $"Spankbang:list:{search}:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out string html))
            {
                html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Spankbang.useproxy, httpversion: 2, addHeaders: new List<(string name, string val)>()
                {
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("pragma", "no-cache"),
                    ("sec-ch-ua", "\"Chromium\";v=\"110\", \"Not A(Brand\";v=\"24\", \"Google Chrome\";v=\"110\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1")
                });

                if (html == null || !html.Contains("<div class=\"video-item"))
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
                        playlist_url = $"{host}/sbg",
                    },
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новое" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Новое",
                                playlist_url = $"{host}/sbg"
                            },
                            new MenuItem()
                            {
                                title = "Трендовое",
                                playlist_url = $"{host}/sbg?sort=trending_videos"
                            },
                            new MenuItem()
                            {
                                title = "Популярное",
                                playlist_url = $"{host}/sbg?sort=most_popular"
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

            foreach (string row in Regex.Split(html, "<div class=\"video-item").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || !row.Contains("<div class=\"stats\">"))
                    continue;

                string link = Regex.Match(row, "<a href=\"/([^\"]+)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                string title = Regex.Match(row, "class=\"n\">([^<]+)<", RegexOptions.IgnoreCase).Groups[1].Value;

                if (!string.IsNullOrWhiteSpace(link) && !string.IsNullOrWhiteSpace(title))
                {
                    string quality = new Regex("<span class=\"h\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                    string duration = new Regex("<span class=\"l\">([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    string img = new Regex("data-src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                    playlists.Add(new PlaylistItem()
                    {
                        name = title,
                        video = $"{host}/sbg/vidosik?goni={HttpUtility.UrlEncode(link)}",
                        quality = string.IsNullOrWhiteSpace(quality) ? null : quality,
                        picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, img),
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
