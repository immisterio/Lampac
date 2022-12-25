using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.AniLibria;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.LITE
{
    public class AniLibriaOnline : BaseController
    {
        [HttpGet]
        [Route("lite/anilibria")]
        async public Task<ActionResult> Index(string title, string code, int year)
        {
            if (!AppInit.conf.AnilibriaOnline.enable || string.IsNullOrWhiteSpace(title))
                return Content(string.Empty);

            #region Кеш
            string memkey = $"aniLibriaonline:{title}";

            if (!memoryCache.TryGetValue(memkey, out List<RootObject> result))
            {
                var search = await HttpClient.Get<List<RootObject>>($"{AppInit.conf.AnilibriaOnline.apihost}/v2/searchTitles?search=" + HttpUtility.UrlEncode(title), useproxy: AppInit.conf.AnilibriaOnline.useproxy, IgnoreDeserializeObject: true);
                if (search == null || search.Count == 0)
                    return Content(string.Empty);

                result = new List<RootObject>();
                foreach (var item in search)
                {
                    if (item.names.ru != null && item.names.ru.ToLower().StartsWith(title.ToLower()))
                        result.Add(item);
                }

                memoryCache.Set(memkey, result, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }
            #endregion

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (!string.IsNullOrWhiteSpace(code) || (result.Count == 1 && result[0].season.year == year && result[0].names.ru?.ToLower() == title.ToLower()))
            {
                #region Серии
                var root = string.IsNullOrWhiteSpace(code) ? result[0] : result.Find(i => i.code == code);

                foreach (var episode in root.player.playlist.Select(i => i.Value))
                {
                    #region streansquality
                    string streansquality = string.Empty;

                    foreach (var f in new List<(string quality, string url)> { ("1080p", episode.hls.fhd), ("720p", episode.hls.hd), ("480p", episode.hls.sd) })
                    {
                        if (string.IsNullOrWhiteSpace(f.url))
                            continue;

                        streansquality += $"\"{f.quality}\":\"" + AppInit.HostStreamProxy(HttpContext, AppInit.conf.AnilibriaOnline.streamproxy, f.url) + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                    #endregion

                    string hls = episode.hls.fhd ?? episode.hls.hd ?? episode.hls.sd;
                    hls = AppInit.HostStreamProxy(HttpContext, AppInit.conf.AnilibriaOnline.streamproxy, $"https://{root.player.host}{hls}");

                    string season = string.IsNullOrWhiteSpace(code) || (root.names.ru?.ToLower() == title.ToLower()) ? "1"  : "0";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + season + "\" e=\"" + episode.serie + "\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + $"{title} ({episode.serie} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.serie} серия" + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Поиск
                foreach (var root in result)
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/anilibria?title={HttpUtility.UrlEncode(title)}&code={root.code}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\",\"similar\":true}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{root.names.ru ?? root.names.en} ({root.season.year})" + "</div></div></div>";
                    firstjson = false;
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
