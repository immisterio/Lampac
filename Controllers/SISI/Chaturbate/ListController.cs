using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Models.SISI;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.Chaturbate
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("chu")]
        async public Task<JsonResult> Index(string sort, int pg = 1)
        {
            if (!AppInit.conf.Chaturbate.enable)
                return OnError("disable");

            #region Страница запроса
            string url = AppInit.conf.Chaturbate.host;

            if (!string.IsNullOrWhiteSpace(sort))
                url += $"/{sort}/";

            if (pg > 1)
                url += $"?page={pg}";
            #endregion

            string html = await HttpClient.Get(url, useproxy: AppInit.conf.Chaturbate.useproxy);
            if (html == null)
                return OnError("html");

            var playlists = new List<PlaylistItem>();

            foreach (string row in html.Split("class=\"room_list_room\"").Skip(1))
            {
                var g = new Regex("data-room=\"([^\"]+)\"([^>]+)?>([^<]+)</a>", RegexOptions.IgnoreCase).Match(row).Groups;
                if (row.Contains(">Private</li>") || string.IsNullOrWhiteSpace(g[1].Value) || string.IsNullOrWhiteSpace(g[3].Value))
                    continue;

                string img = new Regex("<img src=\"([^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;

                playlists.Add(new PlaylistItem()
                {
                    name = g[3].Value.Trim(),
                    quality = row.Contains(">HD+</div>") ? "HD+" : row.Contains(">HD</div>") ? "HD" : null,
                    video = $"{AppInit.Host(HttpContext)}/chu/potok.m3u8?baba={HttpUtility.UrlEncode(g[1].Value)}",
                    picture = AppInit.conf.Chaturbate.streamproxy ? $"{AppInit.Host(HttpContext)}/proxyimg/{img}" : img
                });
            }

            if (playlists.Count == 0)
                return OnError("playlists");

            return new JsonResult(new
            {
                menu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "лучшие" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Лучшие",
                                playlist_url = $"{AppInit.Host(HttpContext)}/chu"
                            },
                            new MenuItem()
                            {
                                title = "Девушки",
                                playlist_url = $"{AppInit.Host(HttpContext)}/chu?sort=female-cams"
                            },
                            new MenuItem()
                            {
                                title = "Пары",
                                playlist_url = $"{AppInit.Host(HttpContext)}/chu?sort=couple-cams"
                            },
                            new MenuItem()
                            {
                                title = "Парни",
                                playlist_url = $"{AppInit.Host(HttpContext)}/chu?sort=male-cams"
                            },
                            new MenuItem()
                            {
                                title = "Транссексуалы",
                                playlist_url = $"{AppInit.Host(HttpContext)}/chu?sort=trans-cams"
                            }
                        }
                    }
                },
                list = playlists
            });
        }
    }
}
