using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;

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

            string html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Spankbang.useproxy);
            if (html == null)
                return OnError("html");

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
                        playlist_url = $"{AppInit.Host(HttpContext)}/sbg",
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
                                playlist_url = $"{AppInit.Host(HttpContext)}/sbg"
                            },
                            new MenuItem()
                            {
                                title = "Трендовое",
                                playlist_url = $"{AppInit.Host(HttpContext)}/sbg?sort=trending_videos"
                            },
                            new MenuItem()
                            {
                                title = "Популярное",
                                playlist_url = $"{AppInit.Host(HttpContext)}/sbg?sort=most_popular"
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
                        video = $"{AppInit.Host(HttpContext)}/sbg/vidosik?goni={HttpUtility.UrlEncode(link)}",
                        quality = string.IsNullOrWhiteSpace(quality) ? null : quality,
                        picture = img,
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
