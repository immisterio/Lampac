using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Online.Controllers
{
    public class IframeVideo : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager(AppInit.conf.IframeVideo);

        [HttpGet]
        [Route("lite/iframevideo")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title)
        {
            var init = await loadKit(AppInit.conf.IframeVideo);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var frame = await iframe(imdb_id, kinopoisk_id);
            if (frame.type == null || (frame.type != "movie" && frame.type != "anime"))
                return OnError();

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            var match = new Regex("<a href='/[^/]+/([^/]+)/iframe[^']+' [^>]+><span title='[^']+'>([^<]+)</span>").Match(frame.content);
            while (match.Success)
            {
                if (!string.IsNullOrWhiteSpace(match.Groups[1].Value))
                {
                    string link = $"{host}/lite/iframevideo/video?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&type={frame.type}&cid={frame.cid}&token={match.Groups[1].Value}";
                    string streamlink = $"{link.Replace("/video", "/video.m3u8")}&play=true";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"stream\":\"" + streamlink + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + match.Groups[2].Value + "</div></div>";
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
                string streamlink = $"{link.Replace("/video", "/video.m3u8")}&play=true";

                html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"stream\":\"" + streamlink + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>";
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region Video
        [HttpGet]
        [Route("lite/iframevideo/video")]
        [Route("lite/iframevideo/video.m3u8")]
        async public Task<ActionResult> Video(string type, int cid, string token, string title, string original_title, bool play)
        {
            var init = await loadKit(AppInit.conf.IframeVideo);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            var proxy = proxyManager.Get();

            string memKey = $"iframevideo:view:video:{type}:{cid}:{token}";
            if (!hybridCache.TryGetValue(memKey, out string urim3u8))
            {
                string json = await Http.Post($"{init.cdnhost}/loadvideo", $"token={token}&type={type}&season=&episode=&mobile=false&id={cid}&qt=480", timeoutSeconds: 10, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                    ("DNT", "1"),
                    ("Origin", init.cdnhost),
                    ("P-REF", string.Empty),
                    ("Referer", $"{init.cdnhost}/"),
                    ("Sec-Fetch-Dest", "empty"),
                    ("Sec-Fetch-Mode", "cors"),
                    ("Sec-Fetch-Site", "same-origin"),
                    ("X-REF", $"{init.host}/"),
                    ("sec-ch-ua", "\"Google Chrome\";v=\"113\", \"Chromium\";v=\"113\", \"Not-A.Brand\";v=\"24\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\"")
                )));

                urim3u8 = Regex.Match(json ?? "", "{\"src\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(urim3u8))
                    return OnError(proxyManager);

                hybridCache.Set(memKey, urim3u8, cacheTime(20, init: init));
            }

            string url = HostStreamProxy(init, urim3u8, proxy: proxy);
            if (play)
                return RedirectToPlay(url);

            return Content("{\"method\":\"play\",\"url\":\"" + url + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region iframe
        async ValueTask<(string content, string type, int cid, string path)> iframe(string imdb_id, long kinopoisk_id)
        {
            var init = AppInit.conf.IframeVideo;

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return (null, null, 0, null);

            string memKey = $"iframevideo:view:{imdb_id}:{kinopoisk_id}";

            if (!hybridCache.TryGetValue(memKey, out (string content, string type, int cid, string path) res))
            {
                string uri = $"{init.apihost}/api/v2/search?imdb={imdb_id}&kp={kinopoisk_id}";
                if (!string.IsNullOrWhiteSpace(init.token))
                    uri = $"{init.apihost}/api/v2/movies?kp={kinopoisk_id}&imdb={imdb_id}&api_token={init.token}";

                var proxy = proxyManager.Get();
                var root = await Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
                if (root == null)
                    return (null, null, 0, null);

                var item = root.Value<JArray>("results")?[0];
                if (item == null)
                    return (null, null, 0, null);

                res.cid = item.Value<int>("cid");
                res.path = item.Value<string>("path");
                res.type = item.Value<string>("type");

                res.content = await Http.Get(res.path, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                    ("DNT", "1"),
                    ("Referer", $"{init.host}/"),
                    ("Sec-Fetch-Dest", "iframe"),
                    ("Sec-Fetch-Mode", "navigate"),
                    ("Sec-Fetch-Site", "cross-site"),
                    ("Upgrade-Insecure-Requests", "1"),
                    ("sec-ch-ua", "\"Google Chrome\";v=\"113\", \"Chromium\";v=\"113\", \"Not-A.Brand\";v=\"24\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\"")
                )));

                if (res.content == null)
                {
                    proxyManager.Refresh();
                    return (null, null, 0, null);
                }

                hybridCache.Set(memKey, res, cacheTime(20, init: init));
            }

            return res;
        }
        #endregion
    }
}
