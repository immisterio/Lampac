using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
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
        async public Task<ActionResult> Index(string search, int pg = 1)
        {
            var init = AppInit.conf.Tizam.Clone();

            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return OnError(error_msg, false);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            if (!string.IsNullOrEmpty(search))
                return OnError("no search", false);

            string memKey = $"tizam:{pg}";
            if (!hybridCache.TryGetValue(memKey, out List<PlaylistItem> playlists))
            {
                var proxyManager = new ProxyManager("tizam", init);
                var proxy = proxyManager.Get();

                reset: var rch = new RchClient(HttpContext, host, init, requestInfo);
                if (rch.IsNotSupport("web", out string rch_error))
                    return OnError(rch_error, false);

                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string uri = $"{init.corsHost()}/fil_my_dlya_vzroslyh/s_russkim_perevodom/";

                int page = pg - 1;
                if (page > 0)
                    uri += $"?p={page}";

                string html = rch.enable ? await rch.Get(init.cors(uri), httpHeaders(init)) : 
                                           await HttpClient.Get(init.cors(uri), timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init));

                playlists = Playlist(html);

                if (playlists.Count == 0)
                {
                    if (IsRhubFallback(init))
                        goto reset;

                    return OnError("playlists", proxyManager, !rch.enable);
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, playlists, cacheTime(60, init: init));
            }

            return OnResult(playlists, null, plugin: "tizam");
        }


        List<PlaylistItem> Playlist(string html)
        {
            var playlists = new List<PlaylistItem>() { Capacity = 25 };
            if (string.IsNullOrEmpty(html))
                return playlists;

            foreach (string row in Regex.Split(html.Split("id=\"pagination\"")[0], "video-item").Skip(1))
            {
                if (row.Contains("pin--premium"))
                    continue;

                string title = Regex.Match(row, "-name=\"name\">([^<]+)<").Groups[1].Value;
                string href = Regex.Match(row, "href=\"/([^\"]+)\" itemprop=\"url\"").Groups[1].Value;

                if (!string.IsNullOrEmpty(href) && !string.IsNullOrWhiteSpace(title))
                {
                    string duration = Regex.Match(row, "itemprop=\"duration\" content=\"([^<]+)\"").Groups[1].Value;

                    string img = Regex.Match(row, "class=\"item__img\" src=\"/([^\"]+)\"").Groups[1].Value;
                    if (string.IsNullOrEmpty(img))
                        continue;

                    var pl = new PlaylistItem()
                    {
                        name = title,
                        video = $"tizam/vidosik?uri={HttpUtility.UrlEncode(href)}",
                        picture = $"{AppInit.conf.Tizam.host}/{img}",
                        time = duration?.Trim(),
                        bookmark = new Bookmark()
                        {
                            site = "tizam",
                            href = href,
                            image = $"{AppInit.conf.Tizam.host}/{img}"
                        }
                    };

                    playlists.Add(pl);
                }
            }

            return playlists;
        }
    }
}
