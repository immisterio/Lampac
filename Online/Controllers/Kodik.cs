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
using System.Security.Cryptography;
using System.Text;
using Lampac.Models.LITE.Kodik;
using Shared.Engine.CORE;

namespace Lampac.Controllers.LITE
{
    public class Kodik : BaseController
    {
        ProxyManager proxyManager = new ProxyManager("kodik", AppInit.conf.Kodik);

        [HttpGet]
        [Route("lite/kodik")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string kid, int s = -1)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.Kodik.token))
                return Content(string.Empty);

            if (kinopoisk_id == 0 && string.IsNullOrWhiteSpace(imdb_id))
                return Content(string.Empty);

            JToken results = await search(imdb_id, kinopoisk_id, s);
            if (results == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (results[0].Value<string>("type") is "foreign-movie" or "soviet-cartoon" or "foreign-cartoon" or "russian-cartoon" or "anime" or "russian-movie")
            {
                #region Фильм
                foreach (var data in results)
                {
                    string link = data.Value<string>("link");
                    string voice = data.Value<JObject>("translation").Value<string>("title");

                    string url = $"{host}/lite/kodik/video?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&link={HttpUtility.UrlEncode(link)}";
                    string streamlink = $"{url.Replace("/video", "/video.m3u8")}&play=true";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + url + "\",\"stream\":\"" + streamlink + "\",\"title\":\"" + $"{title ?? original_title} ({voice})" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + voice + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    foreach (var item in results.Reverse())
                    {
                        int season = item.Value<int>("last_season");
                        string link = $"{host}/lite/kodik?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season}";

                        if (html.Contains($"{season} сезон"))
                            continue;

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season} сезон" + "</div></div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    #region Перевод
                    foreach (var item in results)
                    {
                        string id = item.Value<string>("id");
                        if (string.IsNullOrWhiteSpace(id))
                            continue;

                        string name = item.Value<JObject>("translation").Value<string>("title") ?? "оригинал";
                        if (html.Contains(name) || !results.First(i => i.Value<string>("id") == id).Value<JObject>("seasons").ToObject<Dictionary<string, Season>>().ContainsKey(s.ToString()))
                            continue;

                        if (string.IsNullOrWhiteSpace(kid))
                            kid = id;

                        string link = $"{host}/lite/kodik?imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&kid={id}";

                        html += "<div class=\"videos__button selector " + (kid == id ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + name + "</div>";
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    foreach (var episode in results.First(i => i.Value<string>("id") == kid).Value<JObject>("seasons").ToObject<Dictionary<string, Season>>()[s.ToString()].episodes)
                    {
                        string url = $"{host}/lite/kodik/video?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&link={HttpUtility.UrlEncode(episode.Value)}&episode={episode.Key}";
                        string streamlink = $"{url.Replace("/video", "/video.m3u8")}&play=true";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"call\",\"url\":\"" + url + "\",\"stream\":\"" + streamlink + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                        firstjson = false;
                    }
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }

        #region Video - API
        [HttpGet]
        [Route("lite/kodik/video")]
        [Route("lite/kodik/video.m3u8")]
        async public Task<ActionResult> VideoAPI(string title, string original_title, string link, int episode, string account_email, bool play)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.Kodik.secret_token))
            {
                string uri = play ? "videoparse.m3u8" : "videoparse";
                return LocalRedirect($"/lite/kodik/{uri}?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&link={HttpUtility.UrlEncode(link)}&episode={episode}&account_email={HttpUtility.UrlEncode(account_email)}&play={play}");
            }

            string userIp = HttpContext.Connection.RemoteIpAddress.ToString();
            if (AppInit.conf.Kodik.localip)
            {
                userIp = await mylocalip();
                if (userIp == null)
                    return Content(string.Empty);
            }

            string memKey = $"kodik:view:stream:{link}:{userIp}";
            if (!memoryCache.TryGetValue(memKey, out List<(string q, string url)> streams))
            {
                string deadline = DateTime.Now.AddHours(1).ToString("yyyy MM dd HH").Replace(" ", "");
                string hmac = HMAC(AppInit.conf.Kodik.secret_token, $"{link}:{userIp}:{deadline}");

                string json = await HttpClient.Get($"{AppInit.conf.Kodik.linkhost}/api/video-links" + $"?link={link}&p={AppInit.conf.Kodik.token}&ip={userIp}&d={deadline}&s={hmac}", timeoutSeconds: 8);

                streams = new List<(string q, string url)>();
                var match = new Regex("\"([0-9]+)p?\":{\"Src\":\"(https?:)?//([^\"]+)\"", RegexOptions.IgnoreCase).Match(json);
                while (match.Success)
                {
                    if (!string.IsNullOrWhiteSpace(match.Groups[3].Value))
                        streams.Insert(0, ($"{match.Groups[1].Value}p", $"http://{match.Groups[3].Value}"));

                    match = match.NextMatch();
                }

                if (streams.Count == 0)
                    return Content(string.Empty);

                memoryCache.Set(memKey, streams, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            string streansquality = string.Empty;
            foreach (var l in streams)
            {
                string hls = HostStreamProxy(AppInit.conf.Kodik.streamproxy, l.url);
                streansquality += $"\"{l.q}\":\"" + hls + "\",";
            }

            string name = title ?? original_title;
            if (episode > 0)
                name += $" ({episode} серия)";

            if (play)
                return Redirect(streams[0].url);

            return Content("{\"method\":\"play\",\"url\":\"" + streams[0].url + "\",\"title\":\"" + name + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}", "application/json; charset=utf-8");
        }
        #endregion

        #region Video - Parse
        [HttpGet]
        [Route("lite/kodik/videoparse")]
        [Route("lite/kodik/videoparse.m3u8")]
        async public Task<ActionResult> VideoParse(string title, string original_title, string link, int episode, bool play)
        {
            string memKey = $"kodik:view:VideoParse:{link}";
            if (!memoryCache.TryGetValue(memKey, out List<(string q, string url)> streams))
            {
                var proxy = proxyManager.Get();

                string iframe = await HttpClient.Get($"http:{link}", referer: "https://animego.org/", proxy: proxy, timeoutSeconds: 8);
                if (iframe == null)
                {
                    proxyManager.Refresh();
                    return Content(string.Empty);
                }

                string _frame = Regex.Replace(iframe, "[\n\r\t ]+", "");
                string d_sign = new Regex("d_sign=\"([^\"]+)\"").Match(_frame).Groups[1].Value;
                string pd_sign = new Regex("pd_sign=\"([^\"]+)\"").Match(_frame).Groups[1].Value;
                string ref_sign = new Regex("ref_sign=\"([^\"]+)\"").Match(_frame).Groups[1].Value;
                string type = new Regex("videoInfo.type='([^']+)'").Match(_frame).Groups[1].Value;
                string hash = new Regex("videoInfo.hash='([^']+)'").Match(_frame).Groups[1].Value;
                string id = new Regex("videoInfo.id='([^']+)'").Match(_frame).Groups[1].Value;

                string json = await HttpClient.Post($"{AppInit.conf.Kodik.linkhost}/gvi", $"d=animego.org&d_sign={d_sign}&pd=kodik.info&pd_sign={pd_sign}&ref=https%3A%2F%2Fanimego.org%2F&ref_sign={ref_sign}&bad_user=false&type={type}&hash={hash}&id={id}&info=%7B%22advImps%22%3A%7B%7D%7D", proxy: proxy, timeoutSeconds: 8);
                if (json == null)
                {
                    proxyManager.Refresh();
                    return Content(string.Empty);
                }

                streams = new List<(string q, string url)>();

                var match = new Regex("\"([0-9]+)p?\":\\[\\{\"src\":\"([^\"]+)", RegexOptions.IgnoreCase).Match(json);
                while (match.Success)
                {
                    if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
                    {
                        int zCharCode = Convert.ToInt32('Z');

                        string src = Regex.Replace(match.Groups[2].Value, "[a-zA-Z]", e => {
                            int eCharCode = Convert.ToInt32(e.Value[0]);
                            return ((eCharCode <= zCharCode ? 90 : 122) >= (eCharCode = eCharCode + 13) ? (char)eCharCode : (char)(eCharCode - 26)).ToString();
                        });

                        string decodedString = DecodeUrlBase64(src);

                        if (decodedString.StartsWith("//"))
                            decodedString = $"http:{decodedString}";

                        streams.Insert(0, ($"{match.Groups[1].Value}p", decodedString));
                    }

                    match = match.NextMatch();
                }

                if (streams.Count == 0)
                    return Content(string.Empty);

                memoryCache.Set(memKey, streams, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            string streansquality = string.Empty;
            foreach (var l in streams)
            {
                string hls = HostStreamProxy(AppInit.conf.Kodik.streamproxy, l.url);
                streansquality += $"\"{l.q}\":\"" + hls + "\",";
            }

            string name = title ?? original_title;
            if (episode > 0)
                name += $" ({episode} серия)";

            if (play)
                return Redirect(streams[0].url);

            return Content("{\"method\":\"play\",\"url\":\"" + streams[0].url + "\",\"title\":\"" + name + "\", \"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}}", "application/json; charset=utf-8");
        }
        #endregion


        #region search
        async ValueTask<JToken> search(string imdb_id, long kinopoisk_id, int s)
        {
            string memKey = $"kodik:view:{kinopoisk_id}:{imdb_id}:{s}";

            if (!memoryCache.TryGetValue(memKey, out JToken results))
            {
                string url = $"{AppInit.conf.Kodik.apihost}/search?token={AppInit.conf.Kodik.token}&limit=100&with_episodes=true";
                if (kinopoisk_id > 0)
                    url += $"&kinopoisk_id={kinopoisk_id}";

                if (!string.IsNullOrWhiteSpace(imdb_id))
                    url += $"&imdb_id={imdb_id}";

                if (s > 0)
                    url += $"&season={s}";

                var root = await HttpClient.Get<JObject>(url, timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null || !root.ContainsKey("results"))
                {
                    proxyManager.Refresh();
                    return null;
                }

                results = root.GetValue("results");
                if (results.Count() == 0)
                    return null;

                memoryCache.Set(memKey, results, TimeSpan.FromMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return results;
        }
        #endregion

        #region HMAC
        static string HMAC(string key, string message)
        {
            using (var hash = new HMACSHA256(Encoding.UTF8.GetBytes(key)))
            {
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(message))).Replace("-", "").ToLower();
            }
        }
        #endregion

        #region DecodeUrlBase64
        static string DecodeUrlBase64(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/').PadRight(4 * ((s.Length + 3) / 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(s));
        }
        #endregion
    }
}
