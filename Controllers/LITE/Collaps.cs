using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.Collaps;

namespace Lampac.Controllers.LITE
{
    public class Collaps : BaseController
    {
        [HttpGet]
        [Route("lite/collaps")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int s)
        {
            if (kinopoisk_id == 0 || !AppInit.conf.Collaps.enable)
                return Content(string.Empty);

            string content = await embed(kinopoisk_id);
            if (content == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (!content.Contains("seasons:"))
            {
                #region Фильм
                string hls = Regex.Match(content, "hls: +\"(https?://[^\"]+\\.m3u8)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(hls))
                    return Content(string.Empty);

                string audio = Regex.Match(content, "audio: +\\{\"names\":\\[\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(audio))
                    audio = "По умолчанию";

                #region subtitle
                string subtitles = string.Empty;

                try
                {
                    foreach (var cc in JsonConvert.DeserializeObject<List<Cc>>(Regex.Match(content, "cc: +(\\[[^\n\r]+\\]),").Groups[1].Value))
                    {
                        string suburl = AppInit.conf.Collaps.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{cc.url.Replace("https:", "http:")}" : cc.url.Replace("https:", "http:");
                        subtitles += "{\"label\": \"" + cc.name + "\",\"url\": \"" + suburl + "\"},";
                    }
                }
                catch { }

                subtitles = Regex.Replace(subtitles, ",$", "");
                #endregion

                hls = AppInit.conf.Collaps.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{hls}" : hls;
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + audio + "</div></div>";
                #endregion
            }
            else
            {
                #region Сериал
                try
                {
                    var root = JsonConvert.DeserializeObject<List<RootObject>>(Regex.Match(content, "seasons:([^\n\r]+)").Groups[1].Value);

                    if (s == 0)
                    {
                        foreach (var season in root.AsEnumerable().Reverse())
                        {
                            string link = $"{AppInit.Host(HttpContext)}/lite/collaps?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.season}";

                            html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.season} сезон" + "</div></div></div>";
                            firstjson = false;
                        }
                    }
                    else
                    {
                        foreach (var episode in root.First(i => i.season == s).episodes)
                        {
                            #region subtitle
                            string subtitles = string.Empty;

                            if (episode.cc != null && episode.cc.Count > 0)
                            {
                                foreach (var cc in episode.cc)
                                {
                                    string suburl = AppInit.conf.Collaps.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{cc.url.Replace("https:", "http:")}" : cc.url.Replace("https:", "http:");
                                    subtitles += "{\"label\": \"" + cc.name + "\",\"url\": \"" + suburl + "\"},";
                                }
                            }

                            subtitles = Regex.Replace(subtitles, ",$", "");
                            #endregion

                            string file = AppInit.conf.Collaps.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{episode.hls.Replace("https:", "http:")}" : episode.hls.Replace("https:", "http:");
                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.episode + "\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.episode} серия" + "</div></div>";
                            firstjson = false;
                        }
                    }
                }
                catch 
                {
                    return Content(string.Empty);
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async ValueTask<string> embed(long kinopoisk_id)
        {
            string memKey = $"collaps:view:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out string content))
            {
                content = await HttpClient.Get($"{AppInit.conf.Collaps.host}/embed/kp/{kinopoisk_id}", timeoutSeconds: 8, useproxy: AppInit.conf.Collaps.useproxy);
                if (content == null)
                    return null;

                memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }

            return content;
        }
        #endregion
    }
}
