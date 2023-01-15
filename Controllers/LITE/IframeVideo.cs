using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class IframeVideo : BaseController
    {
        [HttpGet]
        [Route("lite/iframevideo")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            if (!AppInit.conf.IframeVideo.enable)
                return Content(string.Empty);

            var frame = await iframe(imdb_id, kinopoisk_id);
            if (frame.type == null || (frame.type != "movie" && frame.type != "anime"))
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            var match = new Regex("<a href='/[^/]+/([^/]+)/iframe[^']+' [^>]+><span title='[^']+'>([^<]+)</span>").Match(frame.content);
            while (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    string link = $"{host}/lite/iframevideo/video?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&type={frame.type}&cid={frame.cid}&token={match.Groups[1].Value}";
                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + match.Groups[2].Value + "</div></div>";
                    firstjson = false;
                }
                match = match.NextMatch();
            }

            if (firstjson)
            {
                string _v = Regex.Match(html, "<span class='muted'><span [^>]+>([^<]+)</span>").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(_v))
                    _v = Regex.Match(html, "<span class='muted'>([^<\n\r]+)").Groups[1].Value;

                string token = Regex.Match(frame.path, "/[^/]+/([^/]+)/iframe").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(token))
                    return Content(string.Empty);

                string voice = string.IsNullOrWhiteSpace(_v) ? "По умолчанию" : _v;
                string link = $"{host}/lite/iframevideo/video?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&type={frame.type}&cid={frame.cid}&token={token}";
                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>";
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }

        #region Video
        [HttpGet]
        [Route("lite/iframevideo/video")]
        async public Task<ActionResult> Video(string type, int cid, string token, string title, string original_title)
        {
            if (!AppInit.conf.IframeVideo.enable)
                return Content(string.Empty);

            string memKey = $"iframevideo:view:video:{type}:{cid}:{token}";
            if (!memoryCache.TryGetValue(memKey, out string urim3u8))
            {
                string json = await HttpClient.Post($"{AppInit.conf.IframeVideo.cdnhost}/loadvideo", $"token={token}&type={type}&season=&episode=&mobile=false&id={cid}&qt=720", timeoutSeconds: 10, useproxy: AppInit.conf.IframeVideo.useproxy, addHeaders: new List<(string name, string val)>()
                {
                    ("Origin", AppInit.conf.IframeVideo.cdnhost),
                    ("Referer", $"{AppInit.conf.IframeVideo.cdnhost}/"),
                    ("Sec-Fetch-Dest", "empty"),
                    ("Sec-Fetch-Mode", "cors"),
                    ("Sec-Fetch-Site", "same-origin"),
                    ("X-REF", "no-referer")
                });

                if (json == null)
                    return Content(string.Empty);

                urim3u8 = Regex.Match(json, "{\"src\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(urim3u8))
                    return Content(string.Empty);

                memoryCache.Set(memKey, urim3u8, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 20 : 5));
            }

            string url = HostStreamProxy(AppInit.conf.IframeVideo.streamproxy, urim3u8);
            return Content("{\"method\":\"play\",\"url\":\"" + url + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion


        #region iframe
        async ValueTask<(string content, string type, int cid, string path)> iframe(string imdb_id, long kinopoisk_id)
        {
            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return (null, null, 0, null);

            string memKey = $"iframevideo:view:{imdb_id}:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out (string content, string type, int cid, string path) res))
            {
                string uri = $"{AppInit.conf.IframeVideo.apihost}/api/v2/search?imdb={imdb_id}&kp={kinopoisk_id}";
                if (!string.IsNullOrWhiteSpace(AppInit.conf.IframeVideo.token))
                    uri = $"{AppInit.conf.IframeVideo.apihost}/api/v2/movies?kp={kinopoisk_id}&imdb={imdb_id}&api_token={AppInit.conf.IframeVideo.token}";

                var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 8);
                if (root == null)
                    return (null, null, 0, null);

                var item = root.Value<JArray>("results")?[0];
                if (item == null)
                    return (null, null, 0, null);

                res.cid = item.Value<int>("cid");
                res.path = item.Value<string>("path");
                res.type = item.Value<string>("type");
                res.content = await HttpClient.Get(res.path, timeoutSeconds: 8);

                if (res.content == null)
                    return (null, null, 0, null);

                memoryCache.Set(memKey, res, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }

            return res;
        }
        #endregion
    }
}
