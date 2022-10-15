using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Xhamster
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("xmr")]
        async public Task<JsonResult> Index(string search, string sort = "newest", int pg = 2)
        {
            if (!AppInit.conf.Xhamster.enable)
                return OnError("disable");

            #region Формируем страницу запроса
            string url = $"{AppInit.conf.Xhamster.host}/{pg}";

            if (!string.IsNullOrWhiteSpace(search))
            {
                url = $"{AppInit.conf.Xhamster.host}/search/{HttpUtility.UrlEncode(search)}?page={pg}";
            }
            else
            {
                if (sort == "newest")
                    url = $"{AppInit.conf.Xhamster.host}/newest/{pg}";

                if (sort == "best")
                    url = $"{AppInit.conf.Xhamster.host}/best/weekly/{pg}";
            }
            #endregion

            string html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Xhamster.useproxy);
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
                        playlist_url = $"{AppInit.Host(HttpContext)}/xmr",
                    },
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) || sort == "trend" ? "в тренде" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "В тренде",
                                playlist_url = $"{AppInit.Host(HttpContext)}/xmr?sort=trend"
                            },
                            new MenuItem()
                            {
                                title = "Самые новые",
                                playlist_url = $"{AppInit.Host(HttpContext)}/xmr?sort=newest"
                            },
                            new MenuItem()
                            {
                                title = "Лучшие видео",
                                playlist_url = $"{AppInit.Host(HttpContext)}/xmr?sort=best"
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

            foreach (string row in (StringConvert.FindLastText(html, "mixed-section") ?? html).Split("<div class=\"thumb-list__item video-thumb").Skip(1))
            {
                if (string.IsNullOrWhiteSpace(row) || row.Contains("badge_premium"))
                    continue;

                var g = new Regex("class=\"video-thumb-info__nam[^\"]+\" href=\"https?://[^/]+/([^\"]+)\"([^>]+)?>(<!--[^-]+-->)?([^<]+)", RegexOptions.IgnoreCase).Match(row).Groups;

                if (!string.IsNullOrWhiteSpace(g[1].Value) && !string.IsNullOrWhiteSpace(g[4].Value))
                {
                    string duration = new Regex("<div class=\"thumb-image-container__duration\">([^<]+)</div>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    if (string.IsNullOrWhiteSpace(duration))
                        duration = new Regex("<span data-role-video-duration>([^<]+)</span>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();

                    string img = new Regex("class=\"thumb-image-container__image\" src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                    playlists.Add(new PlaylistItem()
                    {
                        name = g[4].Value,
                        video = $"{AppInit.Host(HttpContext)}/xmr/vidosik.m3u8?goni={HttpUtility.UrlEncode(g[1].Value)}",
                        picture = img,
                        //quality = row.Contains("_icon--hd") ? "HD" : row.Contains("_icon--uhd") ? "4K" : null,
                        time = duration
                    });
                }
            }

            return playlists;
        }
        #endregion
    }
}
