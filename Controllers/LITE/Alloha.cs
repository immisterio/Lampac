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
    public class Alloha : BaseController
    {
        [HttpGet]
        [Route("lite/alloha")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.Alloha.token))
                return Content(string.Empty);

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return Content(string.Empty);

            JToken data = await search(imdb_id, kinopoisk_id);
            if (data == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (data.Value<int>("category") is 1 or 3)
            {
                #region Фильм
                foreach (var translation in data.Value<JObject>("translation_iframe").ToObject<Dictionary<string, Dictionary<string, object>>>())
                {
                    string link = $"{AppInit.Host(HttpContext)}/lite/alloha/video?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={translation.Key}";
                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + translation.Value["name"].ToString() + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Перевод
                string activTranslate = t;

                foreach (var translation in data.Value<JObject>("translation_iframe").ToObject<Dictionary<string, Dictionary<string, object>>>())
                {
                    if (string.IsNullOrWhiteSpace(activTranslate))
                        activTranslate = translation.Key;

                    string link = $"{AppInit.Host(HttpContext)}/lite/alloha?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={translation.Key}";

                    string active = string.IsNullOrWhiteSpace(t) ? (firstjson ? "active" : "") : (t == translation.Key ? "active" : "");

                    html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation.Value["name"].ToString() + "</div>";
                    firstjson = false;
                }

                html += "</div>";
                #endregion

                #region Сериал
                firstjson = true;
                html += "<div class=\"videos__line\">";

                if (s == -1)
                {
                    foreach (var season in data.Value<JObject>("seasons").ToObject<Dictionary<string, object>>().Reverse())
                    {
                        string link = $"{AppInit.Host(HttpContext)}/lite/alloha?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={activTranslate}&s={season.Key}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    foreach (var episode in data.Value<JObject>("seasons").GetValue(s.ToString()).Value<JObject>("episodes").ToObject<Dictionary<string, object>>().Reverse())
                    {
                        string link = $"{AppInit.Host(HttpContext)}/lite/alloha/video?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&t={t}&s={s}&e={episode.Key}";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                        firstjson = false;
                    }
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }

        #region Video
        [HttpGet]
        [Route("lite/alloha/video")]
        async public Task<ActionResult> Video(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s, int e)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.Alloha.token))
                return Content(string.Empty);

            string userIp = HttpContext.Connection.RemoteIpAddress.ToString();
            if (AppInit.conf.Alloha.localip)
            {
                userIp = await mylocalip();
                if (userIp == null)
                    return Content(string.Empty);
            }

            string memKey = $"alloha:view:stream:{imdb_id}:{kinopoisk_id}:{t}:{s}:{e}:{userIp}";
            if (!memoryCache.TryGetValue(memKey, out (string m3u8, string subtitle) _cache))
            {
                #region url запроса
                string uri = $"{AppInit.conf.Alloha.linkhost}/link_file.php?secret_token={AppInit.conf.Alloha.secret_token}&imdb={imdb_id}&kp={kinopoisk_id}";

                uri += $"&ip={userIp}&translation={t}";

                if (s > 0)
                    uri += $"&season={s}";

                if (e > 0)
                    uri += $"&episode={e}";
                #endregion

                string json = await HttpClient.Get(uri, timeoutSeconds: 8);
                if (json == null || !json.Contains("\"status\":\"success\""))
                    return Content(string.Empty);

                _cache.m3u8 = Regex.Match(json.Replace("\\", ""), "\"playlist_file\":\"\\{[^\\}]+\\}(https?://[^;\"]+\\.m3u8)").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(_cache.m3u8))
                {
                    _cache.m3u8 = Regex.Match(json.Replace("\\", ""), "\"playlist_file\":\"(https?://[^;\"]+\\.m3u8)").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(_cache.m3u8))
                        return Content(string.Empty);
                }

                _cache.subtitle = Regex.Match(json.Replace("\\", ""), "\"subtitle\":\"(https?://[^;\" ]+)").Groups[1].Value;

                memoryCache.Set(memKey, _cache, TimeSpan.FromMinutes(10));
            }

            string subtitles = "{\"label\": \"По умолчанию\",\"url\": \"" + _cache.subtitle + "\"},";

            return Content("{\"method\":\"play\",\"url\":\"" + _cache.m3u8 + "\",\"title\":\"" + (title ?? original_title) + "\", \"subtitles\": [" + Regex.Replace(subtitles, ",$", "") + "]}", "application/json; charset=utf-8");
        }
        #endregion


        #region search
        async ValueTask<JToken> search(string imdb_id, long kinopoisk_id)
        {
            string memKey = $"alloha:view:{kinopoisk_id}:{imdb_id}";

            if (!memoryCache.TryGetValue(memKey, out JToken data))
            {
                var root = await HttpClient.Get<JObject>($"{AppInit.conf.Alloha.apihost}/?token={AppInit.conf.Alloha.token}&kp={kinopoisk_id}&imdb={imdb_id}", timeoutSeconds: 8);
                if (root == null || !root.ContainsKey("data"))
                    return null;

                data = root.GetValue("data");
                memoryCache.Set(memKey, data, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return data;
        }
        #endregion
    }
}
