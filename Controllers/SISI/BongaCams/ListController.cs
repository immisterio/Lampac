using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Model.SISI.BongaCams;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.BongaCams
{
    public class ListController : BaseController
    {
        [HttpGet]
        [Route("bgs")]
        async public Task<JsonResult> Index(string sort, int pg = 1)
        {
            if (!AppInit.conf.BongaCams.enable)
                return OnError("disable");

            if (1 > pg)
                pg = 1;

            int offset = 0;
            if (pg > 1)
                offset = (pg - 1) * 50;

            string memKey = $"BongaCams:list:{sort}:{pg}";
            if (!memoryCache.TryGetValue(memKey, out Listing root))
            {
                // Страница запроса
                string url = $"{AppInit.conf.BongaCams.host}/tools/listing_v3.php?livetab={sort ?? "new"}&online_only=true&offset={offset}";

                // Получаем json
                root = await HttpClient.Get<Listing>(url, useproxy: AppInit.conf.BongaCams.useproxy, addHeaders: new List<(string name, string val)>()
                {
                    ("dnt", "1"),
                    ("referer", AppInit.conf.BongaCams.host),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-requested-with", "XMLHttpRequest")
                });

                if (root?.models == null || root.models.Count == 0)
                    return OnError("models");

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 5 : 1));
            }

            var playlists = new List<PlaylistItem>();

            foreach (var model in root.models)
            {
                // !model.online - всегда false
                if (model.room != "public" || model.is_away)
                    continue;

                string img = $"https:{model.thumb_image.Replace("{ext}", "jpg")}";

                playlists.Add(new PlaylistItem()
                {
                    name = model.display_name,
                    quality = model.hd_plus == 1 ? "HD+" : model.hd_cam == 1 ? "HD" : null,
                    video = $"{AppInit.Host(HttpContext)}/bgs/potok.m3u8?baba={HttpUtility.UrlEncode(model.username)}",
                    picture = $"{AppInit.Host(HttpContext)}/proxyimg/{img}",
                });
            }

            return new JsonResult(new
            {
                menu = new List<MenuItem>()
                {
                    new MenuItem()
                    {
                        title = $"Сортировка: {(string.IsNullOrWhiteSpace(sort) ? "новые" : sort)}",
                        playlist_url = "submenu",
                        submenu = new List<MenuItem>()
                        {
                            new MenuItem()
                            {
                                title = "Новые",
                                playlist_url = $"{AppInit.Host(HttpContext)}/bgs"
                            },
                            new MenuItem()
                            {
                                title = "Пары",
                                playlist_url = $"{AppInit.Host(HttpContext)}/bgs?sort=couples"
                            },
                            new MenuItem()
                            {
                                title = "Девушки",
                                playlist_url = $"{AppInit.Host(HttpContext)}/bgs?sort=female"
                            },
                            new MenuItem()
                            {
                                title = "Парни",
                                playlist_url = $"{AppInit.Host(HttpContext)}/bgs?sort=male"
                            },
                            new MenuItem()
                            {
                                title = "Транссексуалы",
                                playlist_url = $"{AppInit.Host(HttpContext)}/bgs?sort=transsexual"
                            }
                        }
                    }
                },
                list = playlists
            });
        }
    }
}
