using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lampac.Engine.CORE;
using Online;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class Bazon : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("bazon", AppInit.conf.Bazon);

        [HttpGet]
        [Route("lite/bazon")]
        async public Task<ActionResult> Index(long kinopoisk_id, bool checksearch, string title, string original_title, string t, int s = -1)
        {
            if (kinopoisk_id == 0 || !AppInit.conf.Bazon.enable)
                return OnError();

            if (checksearch)
            {
                if (await search(kinopoisk_id))
                    return Content("data-json=");

                return OnError(proxyManager);
            }

            string userIp = HttpContext.Connection.RemoteIpAddress.ToString();

            if (AppInit.conf.Bazon.localip)
            {
                userIp = await mylocalip();
                if (userIp == null)
                    return OnError();
            }

            string memKey = $"bazon:view:{kinopoisk_id}:{userIp}";
            if (!memoryCache.TryGetValue(memKey, out JToken results))
            {
                var root = await HttpClient.Get<JObject>($"{AppInit.conf.Bazon.apihost}/api/playlist?token={AppInit.conf.Bazon.token}&kp={kinopoisk_id}&ref=&ip={userIp}", timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null || !root.ContainsKey("results"))
                    return OnError(proxyManager);

                results = root.GetValue("results");
                memoryCache.Set(memKey, results, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (results[0].Value<string>("serial") == "0")
            {
                #region Фильм
                foreach (var video in results)
                {
                    var playlists = video.Value<JObject>("playlists").ToObject<Dictionary<string, string>>().Reverse();

                    #region getQualitys
                    string getQualitys()
                    {
                        string streansquality = string.Empty;

                        foreach (var link in playlists)
                        {
                            streansquality += $"\"{link.Key}p\":\"" + link.Value + "\",";
                        }

                        return "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";
                    }
                    #endregion

                    string translation = video.Value<string>("translation");

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + playlists.First().Value + "\",\"title\":\"" + $"{title ?? original_title} ({translation})" + "\", " + getQualitys() + ", \"voice_name\":\"" + translation + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{playlists.First().Key}p" + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    foreach (var item in results)
                    {
                        foreach (var season in item.Value<JObject>("playlists").ToObject<Dictionary<string, object>>())
                        {
                            string link = $"{host}/lite/bazon?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.Key}";

                            html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
                            firstjson = false;
                        }

                        break;
                    }
                }
                else
                {
                    #region Перевод
                    string activTranslate = t;

                    foreach (var item in results)
                    {
                        string translation = item?.Value<string>("studio") ?? item.Value<string>("translation");
                        if (string.IsNullOrWhiteSpace(activTranslate))
                            activTranslate = translation;

                        string link = $"{host}/lite/bazon?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={HttpUtility.UrlEncode(translation)}";

                        html += "<div class=\"videos__button selector " + (activTranslate == translation ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation + "</div>";
                    }

                    firstjson = true;
                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    foreach (var item in results)
                    {
                        if ((item?.Value<string>("studio") ?? item.Value<string>("translation")) != activTranslate)
                            continue;

                        foreach (var episode in item.Value<JObject>("playlists").GetValue(s.ToString()).ToObject<Dictionary<string, Dictionary<string, string>>>())
                        {
                            string streansquality = string.Empty;
                            List<(string link, string quality)> streams = new List<(string, string)>();

                            foreach (var link in episode.Value.Reverse())
                            {
                                streams.Add((link.Value, $"{link.Key}p"));
                                streansquality += $"\"{link.Key}p\":\"" + link.Value + "\",";
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


        #region search
        async ValueTask<bool> search(long kinopoisk_id)
        {
            string memKey = $"bazon:checksearch:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out bool results))
            {
                var root = await HttpClient.Get<JObject>($"https://bazon.cc/api/search?token={AppInit.conf.Bazon.token}&kp={kinopoisk_id}", timeoutSeconds: 8, proxy: proxyManager.Get());
                results = root != null && root.ContainsKey("results");
                memoryCache.Set(memKey, results, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return results;
        }
        #endregion
    }
}
