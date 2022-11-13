using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class Bazon : BaseController
    {
        [HttpGet]
        [Route("lite/bazon")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, string t, int s = -1)
        {
            if (kinopoisk_id == 0 || string.IsNullOrWhiteSpace(AppInit.conf.Bazon.token))
                return Content(string.Empty);

            string userIp = HttpContext.Connection.RemoteIpAddress.ToString();

            if (AppInit.conf.Bazon.localip)
            {
                userIp = await mylocalip();
                if (userIp == null)
                    return Content(string.Empty);
            }

            string memKey = $"bazon:view:{kinopoisk_id}:{userIp}";
            if (!memoryCache.TryGetValue(memKey, out JToken results))
            {
                var root = await HttpClient.Get<JObject>($"{AppInit.conf.Bazon.apihost}/api/playlist?token={AppInit.conf.Bazon.token}&kp={kinopoisk_id}&ref=&ip={userIp}", timeoutSeconds: 8);
                if (root == null || !root.ContainsKey("results"))
                    return Content(string.Empty);

                results = root.GetValue("results");
                memoryCache.Set(memKey, results, TimeSpan.FromMinutes(10));
            }

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (results[0].Value<string>("serial") == "0")
            {
                #region getQualitys
                string getQualitys(string max)
                {
                    string streansquality = string.Empty;
                    if (!int.TryParse(max, out int _max) || _max == 0)
                        return string.Empty;

                    foreach (var link in results[0].Value<JObject>("playlists").ToObject<Dictionary<string, string>>().Reverse())
                    {
                        if (int.TryParse(link.Key, out int _q) && _q > 0 && _max >= _q)
                            streansquality += $"\"{link.Key}p\":\"" + link.Value.Replace("https:", "http:") + "\",";
                    }

                    return "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                }
                #endregion

                #region Фильм
                string translation = results[0].Value<string>("translation");

                foreach (var link in results[0].Value<JObject>("playlists").ToObject<Dictionary<string, string>>().Reverse())
                {
                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + link.Value + "\",\"title\":\"" + $"{title ?? original_title} ({translation})" + "\", " + getQualitys(link.Key) + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{link.Key}p" + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Перевод
                string activTranslate = t;

                foreach (var item in results)
                {
                    string translation = item.Value<string>("translation");
                    if (string.IsNullOrWhiteSpace(activTranslate))
                        activTranslate = translation;

                    string link = $"{AppInit.Host(HttpContext)}/lite/bazon?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={HttpUtility.UrlEncode(translation)}";

                    string active = string.IsNullOrWhiteSpace(t) ? (firstjson ? "active" : "") : (t == translation ? "active" : "");

                    html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation + "</div>";
                    firstjson = false;
                }

                html += "</div>";
                #endregion

                #region Сериал
                firstjson = true;
                html += "<div class=\"videos__line\">";

                if (s == -1)
                {
                    foreach (var item in results)
                    {
                        if (item.Value<string>("translation") != activTranslate)
                            continue;

                        foreach (var season in item.Value<JObject>("playlists").ToObject<Dictionary<string, object>>())
                        {
                            string link = $"{AppInit.Host(HttpContext)}/lite/bazon?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={HttpUtility.UrlEncode(activTranslate)}&s={season.Key}";

                            html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
                            firstjson = false;
                        }

                        break;
                    }
                }
                else
                {
                    foreach (var item in results)
                    {
                        if (item.Value<string>("translation") != activTranslate)
                            continue;

                        foreach (var episode in item.Value<JObject>("playlists").GetValue(s.ToString()).ToObject<Dictionary<string, Dictionary<string, string>>>())
                        {
                            string streansquality = string.Empty;
                            List<(string link, string quality)> streams = new List<(string, string)>();

                            foreach (var link in episode.Value.Reverse())
                            {
                                streams.Add((link.Value.Replace("https:", "http:"), $"{link.Key}p"));
                                streansquality += $"\"{link.Key}p\":\"" + link.Value.Replace("https:", "http:") + "\",";
                            }

                            streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                            firstjson = false;
                        }

                        break;
                    }
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }
    }
}
