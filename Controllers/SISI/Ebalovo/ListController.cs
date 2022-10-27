using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Ebalovo
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("elo")]
        async public Task<JsonResult> Index(string search, string sort, int pg = 1)
        {
            if (!AppInit.conf.Ebalovo.enable)
                return OnError("disable");

            #region Формируем страницу запроса
            string url = $"{AppInit.conf.Ebalovo.host}/";

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

            var html = await HttpClient.Get(url);
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
                        playlist_url = $"{AppInit.Host(HttpContext)}/elo",
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
                                playlist_url = $"{AppInit.Host(HttpContext)}/elo"
                            },
                            new MenuItem()
                            {
                                title = "Лучшее",
                                playlist_url = $"{AppInit.Host(HttpContext)}/elo?sort=porno-online"
                            },
                            new MenuItem()
                            {
                                title = "Популярное",
                                playlist_url = $"{AppInit.Host(HttpContext)}/elo?sort=xxx-top"
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

            foreach (string row in Regex.Split(Regex.Replace(html, "[\n\r\t]+", ""), "<div class=\"item\">"))
            {
                if (string.IsNullOrWhiteSpace(row) || !row.Contains("<div class=\"item-info\">"))
                    continue;

                string link = new Regex($"<a href=\"https?://[^/]+/(video/[^\"]+)\"", RegexOptions.IgnoreCase).Match(row).Groups[1].Value;
                string title = new Regex("<div class=\"item-title\">([^<]+)</div>", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();

                if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(link))
                {
                    string duration = new Regex(" data-eb=\"([^;\"]+);", RegexOptions.IgnoreCase).Match(row).Groups[1].Value.Trim();
                    var img = new Regex("(class=\"thumb\") src=\"(([^\"]+)/[0-9]+.jpg)\"", RegexOptions.IgnoreCase).Match(row).Groups;
                    if (string.IsNullOrWhiteSpace(img[3].Value) || img[2].Value.Contains("load.png"))
                        img = new Regex("(data-srcset|data-src)=\"(([^\"]+)/[0-9]+.jpg)\"", RegexOptions.IgnoreCase).Match(row).Groups;

                    playlists.Add(new PlaylistItem()
                    {
                        name = title,
                        video = $"{AppInit.Host(HttpContext)}/elo/vidosik?goni={HttpUtility.UrlEncode(link)}",
                        picture = AppInit.conf.Ebalovo.streamproxy ? $"{AppInit.Host(HttpContext)}/proxyimg/{img[2].Value}" : img[2].Value,
                        time = duration
                    });
                }
            }

            return playlists;
        }
        #endregion
    }
}
