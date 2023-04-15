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
        #region ListController
        public static List<(string name, string val)> headers = new List<(string name, string val)>() 
        {
            ("cookie", "cor=CU; coe=ww; cfc_ok=00|2|ww|ru|master|0; backend_version=main; _ga=GA1.3.1461554580.1681547007; _gid=GA1.3.474005340.1681547007; __cf_bm=65YepZYb0ZHsmCe6eJ727djyxo6VKNA3PZ742EpG4VY-1681547006-0-AdKlqSpX8AOmBLABLR9HCfNSUXbRNE0KSyJWKQNyaVbzyk5KW+zdk9WRZAnmaLaEVAC7VncDT9C7W1UQl2rBRJtIc9LuNKxoZX5XoKS/H5Ehd0uXTgVKW/TYSKSXmBx8XkjOBZR/PdX3mbJbQgGU2gaEhjP3cmy7j5XHJDv0y6pq; ana_vid=a6c8f77399dc87da58ec057de8e298274cf478cf6b63c2f8796e84aab05c0ddb; ana_sid=a6c8f77399dc87da58ec057de8e298274cf478cf6b63c2f8796e84aab05c0ddb; age_pass=1; age_pass=1; sb_session=eyJfcGVybWFuZW50Ijp0cnVlfQ.ZDpfSg.oxGXJKdtuvnjs25gejnvfFPVCi0"),
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
        };
        #endregion

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
                html = await HttpClient.Get(url, timeoutSeconds: 10, useproxy: AppInit.conf.Spankbang.useproxy, httpversion: 2, addHeaders: headers);

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
