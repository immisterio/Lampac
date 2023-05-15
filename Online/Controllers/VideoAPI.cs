using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class VideoAPI : BaseController
    {
        [HttpGet]
        [Route("lite/videoapi")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.VideoAPI.token))
                return Content(string.Empty);

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return Content(string.Empty);

            string content = await iframe(imdb_id, kinopoisk_id);
            if (content == null)
                return Content(string.Empty);

            string html = "<div class=\"videos__line\">";

            string streansquality = string.Empty;
            List<(string link, string quality)> streams = new List<(string, string)>();

            foreach (var quality in new List<string> { "1080", "720", "480", "360" })
            {
                string link = new Regex($"//([^/]+/([^/:]+:[0-9]+/)?(movies|animes)/[^\n\r\t, ]+/{quality}.mp4:[^\\.\n\r\t, ]+\\.m3u8)").Match(content).Groups[1].Value;
                if (string.IsNullOrEmpty(link))
                    continue;

                link = $"http://{link}";
                link = HostStreamProxy(AppInit.conf.VideoAPI.streamproxy, link);

                streams.Add((link, $"{quality}p"));
                streansquality += $"\"{quality}p\":\"" + link + "\",";
            }

            html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">ENGLISH</div></div>";

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region iframe
        async ValueTask<string> iframe(string imdb_id, long kinopoisk_id)
        {
            try
            {
                string memKeyIframesrc = $"videoapi:view:iframe_src:{imdb_id}:{kinopoisk_id}";
                if (!memoryCache.TryGetValue(memKeyIframesrc, out string code))
                {
                    var proxyManager = new ProxyManager("videoapi", AppInit.conf.VideoAPI);
                    var proxy = proxyManager.Get();

                    var json = await HttpClient.Get<JObject>($"{AppInit.conf.VideoAPI.corsHost()}/api/short?api_token={AppInit.conf.VideoAPI.token}" + $"&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}", timeoutSeconds: 8, proxy: proxy);
                    if (json == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    string iframe_src = json.Value<JArray>("data").First.Value<string>("iframe_src");
                    if (string.IsNullOrWhiteSpace(iframe_src))
                        return null;

                    string iframe = await HttpClient.Get(AppInit.conf.VideoAPI.corsHost(iframe_src), referer: "https://kinogo.biz/53104-avatar-2-2022.html", httpversion: 2, timeoutSeconds: 8, proxy: proxy);
                    if (iframe == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    code = Regex.Match(iframe, "id=\"files\" value='([^\n\r]+)'>").Groups[1].Value;
                    code = code.Replace("&quot;", "\"").Replace("\\\"", "\"").Replace("\\\\\\", "\\").Replace("\\\\", "\\");
                    code = code.Replace("\\", "");

                    if (string.IsNullOrWhiteSpace(code))
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    memoryCache.Set(memKeyIframesrc, code, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 5));
                }

                return code;
            }
            catch { return null; }
        }
        #endregion
    }
}
