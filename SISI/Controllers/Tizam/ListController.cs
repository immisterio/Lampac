using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Web;
using Lampac.Models.SISI;
using System.Text.RegularExpressions;
using Shared.Model.SISI;
using SISI;
using Shared.Engine.CORE;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.Tizam
{
    public class ListController : BaseSisiController
    {
        [Route("tizam")]
        async public Task<JsonResult> Index(string search, int pg = 1)
        {
            if (!AppInit.conf.Tizam.enable)
                return OnError("disable");

            if (!string.IsNullOrEmpty(search))
                return OnError("no search");

            string memKey = $"tizam:{pg}";
            if (!memoryCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("tizam", AppInit.conf.Tizam);
                var proxy = proxyManager.Get();

                string uri = $"{AppInit.conf.Tizam.corsHost()}/fil_my_dlya_vzroslyh/s_russkim_perevodom/";

                int page = pg - 1;
                if (page > 0)
                    uri += $"?p={page}";

                string html = await HttpClient.Get(uri, timeoutSeconds: 10, proxy: proxy);
                if (html == null)
                    return OnError("html", proxyManager);

                playlists = Playlist(html);

                if (playlists.Count == 0)
                    return OnError("playlists", proxyManager);

                memoryCache.Set(memKey, playlists, cacheTime(60));
            }

            return OnResult(playlists, null);
        }


        List<PlaylistItem> Playlist(string html)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 25 };

            foreach (string row in Regex.Split(html.Split("id=\"pagination\"")[0], "item-video").Skip(1))
            {
                if (row.Contains("class=\"premicon\""))
                    continue;

                string title = Regex.Match(row, "class=\"title\"([^>]+)?>([^<]+)<").Groups[2].Value;
                string href = Regex.Match(row, "href=\"/([^\"]+)\" class=\"link\"").Groups[1].Value;

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = Regex.Match(row, "itemprop=\"duration\" content=\"([^<]+)\"").Groups[1].Value;

                    string img = Regex.Match(row, "class=\"thumb\" src=\"/([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(img))
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"tizam/vidosik?uri={HttpUtility.UrlEncode(href)}",
                        picture = $"{AppInit.conf.Tizam.corsHost()}/{img}",
                        time = duration?.Trim(),
                        bookmark = new Bookmark()
                        {
                            site = "tizam",
                            href = href,
                            image = $"{AppInit.conf.Tizam.corsHost()}/{img}"
                        }
                    };

                    playlists.Add(pl);
                }
            }

            return playlists;
        }
    }
}
