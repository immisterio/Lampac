using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class VideoAPI : BaseController
    {
        [HttpGet]
        [Route("lite/videoapi")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return Content(string.Empty);

            string content = await iframe(memoryCache, imdb_id, kinopoisk_id);
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
                link = AppInit.conf.VideoAPI.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link;

                streams.Add((link, $"{quality}p"));
                streansquality += $"\"{quality}p\":\"" + link + "\",";
            }

            html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">ENGLISH</div></div>";

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region iframe
        async public static ValueTask<string> iframe(IMemoryCache memoryCache, string imdb_id, long kinopoisk_id)
        {
            try
            {
                string memKeyIframesrc = $"videoapi:view:iframe_src:{imdb_id}:{kinopoisk_id}";
                if (!memoryCache.TryGetValue(memKeyIframesrc, out string code))
                {
                    var json = await HttpClient.Get<JObject>($"{AppInit.conf.VideoAPI.apihost}/api/short?api_token={AppInit.conf.VideoAPI.token}" + $"&kinopoisk_id={kinopoisk_id}&imdb_id={imdb_id}", timeoutSeconds: 8, useproxy: AppInit.conf.VideoAPI.useproxy);
                    string iframe_src = json.Value<JArray>("data").First.Value<string>("iframe_src");
                    if (string.IsNullOrWhiteSpace(iframe_src))
                        return null;

                    string iframe = await HttpClient.Get(iframe_src, timeoutSeconds: 8, useproxy: AppInit.conf.VideoAPI.useproxy);
                    if (iframe == null)
                        return null;

                    code = Regex.Match(iframe, ":&quot;([^\n\r]+)&quot;").Groups[1].Value;
                    code = code.Replace("&quot;", "\"").Replace("\\\"", "\"").Replace("\\\\\\", "\\").Replace("\\\\", "\\");
                    code = Regex.Split(code, "\",\"[0-9]+\"")[0];
                    code = code.Replace("\"}\">", "");
                    code = code.Replace("\\", "");

                    if (string.IsNullOrWhiteSpace(code))
                        return null;

                    memoryCache.Set(memKeyIframesrc, code, DateTime.Now.AddMinutes(5));
                }

                return code;
            }
            catch { return null; }
        }
        #endregion
    }
}
