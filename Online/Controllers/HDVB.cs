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
using Lampac.Models.LITE.HDVB;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class HDVB : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("hdvb", AppInit.conf.HDVB);

        [HttpGet]
        [Route("lite/hdvb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int serial, int t = -1, int s = -1)
        {
            if (kinopoisk_id == 0 || !AppInit.conf.HDVB.enable)
                return OnError();

            JArray data = await search(kinopoisk_id);
            if (data == null)
                return OnError(proxyManager);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (serial == 0)
            {
                #region Фильм
                foreach (var m in data)
                {
                    string link = $"{host}/lite/hdvb/video?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={HttpUtility.UrlEncode(m.Value<string>("iframe_url"))}";
                    string streamlink = $"{link.Replace("/video", "/video.m3u8")}&play=true";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"stream\":\"" + streamlink + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + m.Value<string>("translator") + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    foreach (var voice in data)
                    {
                        foreach (var season in voice.Value<JArray>("serial_episodes"))
                        {
                            string season_name = $"{season.Value<int>("season_number")} сезон";
                            if (html.Contains(season_name))
                                continue;

                            string link = $"{host}/lite/hdvb?serial=1&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.Value<int>("season_number")}";

                            html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + season_name + "</div></div></div>";
                            firstjson = false;
                        }
                    }
                }
                else
                {
                    #region Перевод
                    for (int i = 0; i < data.Count; i++)
                    {
                        if (data[i].Value<JArray>("serial_episodes").FirstOrDefault(i => i.Value<int>("season_number") == s) == null)
                            continue;

                        if (t == -1)
                            t = i;

                        string link = $"{host}/lite/hdvb?serial=1&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={i}";

                        html += "<div class=\"videos__button selector " + (t == i ? "active" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + data[i].Value<string>("translator") + "</div>";
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    string iframe = HttpUtility.UrlEncode(data[t].Value<string>("iframe_url"));
                    string translator = HttpUtility.UrlEncode(data[t].Value<string>("translator"));

                    foreach (int episode in data[t].Value<JArray>("serial_episodes").FirstOrDefault(i => i.Value<int>("season_number") == s).Value<JArray>("episodes").ToObject<List<int>>())
                    {
                        string link = $"{host}/lite/hdvb/serial?title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={iframe}&t={translator}&s={s}&e={episode}";
                        string streamlink = $"{link.Replace("/serial", "/serial.m3u8")}&play=true";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"stream\":\"" + streamlink + "\",\"title\":\"" + $"{title ?? original_title} ({episode} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode} серия" + "</div></div>";
                        firstjson = false;
                    }
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region Video
        [HttpGet]
        [Route("lite/hdvb/video")]
        [Route("lite/hdvb/video.m3u8")]
        async public Task<ActionResult> Video(string iframe, string title, string original_title, bool play)
        {
            if (!AppInit.conf.HDVB.enable)
                return OnError();

            var proxy = proxyManager.Get();

            string memKey = $"video:view:video:{iframe}";
            if (!memoryCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await HttpClient.Get(iframe, referer: $"{AppInit.conf.HDVB.host}/", timeoutSeconds: 8, proxy: proxy);
                if (html == null)
                    return OnError(proxyManager);

                string vid = "vid1666694269";
                string href = Regex.Match(html, "\"href\":\"([^\"]+)\"").Groups[1].Value;
                string csrftoken = Regex.Match(html, "\"key\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                string file = Regex.Match(html, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(csrftoken))
                    return OnError(proxyManager);

                var header = new List<(string name, string val)>()
                {
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("origin", $"https://{vid}.{href}"),
                    ("referer", iframe),
                    ("sec-ch-ua", "\"Chromium\";v=\"106\", \"Google Chrome\";v=\"106\", \"Not;A=Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-csrf-token", csrftoken)
                };

                urim3u8 = await HttpClient.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, addHeaders: header);
                if (urim3u8 == null)
                    return OnError(proxyManager);

                if (!urim3u8.Contains("/index.m3u8"))
                {
                    file = Regex.Match(urim3u8, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                    file = Regex.Replace(file, "^/playlist/", "/");
                    file = Regex.Replace(file, "\\.txt$", "");
                    if (string.IsNullOrWhiteSpace(file))
                        return OnError(proxyManager);

                    urim3u8 = await HttpClient.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, addHeaders: header);
                    if (urim3u8 == null)
                        return OnError(proxyManager);
                }

                memoryCache.Set(memKey, urim3u8, cacheTime(20));
            }

            if (play)
                return Redirect(HostStreamProxy(AppInit.conf.HDVB, urim3u8, proxy: proxy));

            return Content("{\"method\":\"play\",\"url\":\"" + HostStreamProxy(AppInit.conf.HDVB, urim3u8, proxy: proxy) + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region Serial
        [HttpGet]
        [Route("lite/hdvb/serial")]
        [Route("lite/hdvb/serial.m3u8")]
        async public Task<ActionResult> Serial(string iframe, string t, string s, string e, string title, string original_title, bool play)
        {
            if (!AppInit.conf.HDVB.enable)
                return Content(string.Empty);

            var proxy = proxyManager.Get();

            string memKey = $"video:view:serial:{iframe}:{t}:{s}:{e}";
            if (!memoryCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await HttpClient.Get(iframe, referer: $"{AppInit.conf.HDVB.host}/", timeoutSeconds: 8, proxy: proxy);
                if (html == null)
                    return OnError(proxyManager);

                #region playlist
                string vid = "vid1666694269";
                string href = Regex.Match(html, "\"href\":\"([^\"]+)\"").Groups[1].Value;
                string csrftoken = Regex.Match(html, "\"key\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                string file = Regex.Match(html, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(csrftoken))
                    return OnError(proxyManager);

                var headers = new List<(string name, string val)>()
                {
                    ("cache-control", "no-cache"),
                    ("dnt", "1"),
                    ("origin", $"https://{vid}.{href}"),
                    ("referer", iframe),
                    ("sec-ch-ua", "\"Chromium\";v=\"106\", \"Google Chrome\";v=\"106\", \"Not;A=Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-origin"),
                    ("x-csrf-token", csrftoken)
                };

                var playlist = await HttpClient.Post<List<Folder>>($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, addHeaders: headers, IgnoreDeserializeObject: true);
                if (playlist == null || playlist.Count == 0)
                    return OnError(proxyManager);
                #endregion

                file = playlist.First(i => i.id == s).folder.First(i => i.episode == e).folder.First(i => i.title == t).file;
                if (string.IsNullOrWhiteSpace(file))
                    return OnError(proxyManager);

                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                urim3u8 = await HttpClient.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, addHeaders: headers);
                if (urim3u8 == null || !urim3u8.Contains("/index.m3u8"))
                    return OnError(proxyManager);

                memoryCache.Set(memKey, urim3u8, cacheTime(20));
            }

            if (play)
                return Redirect(HostStreamProxy(AppInit.conf.HDVB, urim3u8, proxy: proxy));

            return Content("{\"method\":\"play\",\"url\":\"" + HostStreamProxy(AppInit.conf.HDVB, urim3u8, proxy: proxy) + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region search
        async ValueTask<JArray> search(long kinopoisk_id)
        {
            string memKey = $"hdvb:view:{kinopoisk_id}";

            if (!memoryCache.TryGetValue(memKey, out JArray root))
            {
                root = await HttpClient.Get<JArray>($"{AppInit.conf.HDVB.host}/api/videos.json?token={AppInit.conf.HDVB.token}&id_kp={kinopoisk_id}", timeoutSeconds: 8, proxy: proxyManager.Get());
                if (root == null || root.Count == 0)
                    return null;

                memoryCache.Set(memKey, root, cacheTime(40));
            }

            return root;
        }
        #endregion
    }
}
