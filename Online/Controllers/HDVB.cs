using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Linq;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.HDVB;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class HDVB : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("hdvb", AppInit.conf.HDVB);

        [HttpGet]
        [Route("lite/hdvb")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int t = -1, int s = -1)
        {
            if (kinopoisk_id == 0 || !AppInit.conf.HDVB.enable)
                return OnError();

            JArray data = await search(kinopoisk_id);
            if (data == null)
                return OnError();

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (data.First.Value<string>("type") == "movie")
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, data.Count);

                foreach (var m in data)
                {
                    string link = $"{host}/lite/hdvb/video?kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&iframe={HttpUtility.UrlEncode(m.Value<string>("iframe_url"))}";
                    
                    mtpl.Append(m.Value<string>("translator"), link, "call", $"{link.Replace("/video", "/video.m3u8")}&play=true");
                }

                return Content(mtpl.ToHtml(), "text/html; charset=utf-8");
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
            var init = AppInit.conf.HDVB;

            if (!init.enable)
                return OnError();

            var proxy = proxyManager.Get();

            string memKey = $"video:view:video:{iframe}";
            if (!hybridCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await HttpClient.Get(iframe, referer: $"{init.host}/", timeoutSeconds: 8, proxy: proxy);
                if (html == null)
                    return OnError(proxyManager);

                string vid = "vid1666694269";
                string href = Regex.Match(html, "\"href\":\"([^\"]+)\"").Groups[1].Value;
                string csrftoken = Regex.Match(html, "\"key\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                string file = Regex.Match(html, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                if (string.IsNullOrWhiteSpace(href) || string.IsNullOrWhiteSpace(file) || string.IsNullOrWhiteSpace(csrftoken))
                    return OnError();

                var header = httpHeaders(init, HeadersModel.Init(
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
                ));

                urim3u8 = await HttpClient.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: header);
                if (urim3u8 == null)
                    return OnError(proxyManager);

                if (!urim3u8.Contains("/index.m3u8"))
                {
                    file = Regex.Match(urim3u8, "\"file\":\"([^\"]+)\"").Groups[1].Value.Replace("\\", "");
                    file = Regex.Replace(file, "^/playlist/", "/");
                    file = Regex.Replace(file, "\\.txt$", "");
                    if (string.IsNullOrWhiteSpace(file))
                        return OnError();

                    urim3u8 = await HttpClient.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: header);
                    if (urim3u8 == null)
                        return OnError(proxyManager);
                }

                proxyManager.Success();
                hybridCache.Set(memKey, urim3u8, cacheTime(20, init: init));
            }

            string m3u8 = HostStreamProxy(init, urim3u8, proxy: proxy, plugin: "hdvb");

            if (play)
                return Redirect(m3u8);

            return Content("{\"method\":\"play\",\"url\":\"" + m3u8 + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region Serial
        [HttpGet]
        [Route("lite/hdvb/serial")]
        [Route("lite/hdvb/serial.m3u8")]
        async public Task<ActionResult> Serial(string iframe, string t, string s, string e, string title, string original_title, bool play)
        {
            var init = AppInit.conf.HDVB;

            if (!init.enable)
                return OnError();

            var proxy = proxyManager.Get();

            string memKey = $"video:view:serial:{iframe}:{t}:{s}:{e}";
            if (!hybridCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await HttpClient.Get(iframe, referer: $"{init.host}/", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));
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
                    return OnError();

                var headers = httpHeaders(init, HeadersModel.Init(
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
                ));

                var playlist = await HttpClient.Post<List<Folder>>($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: headers, IgnoreDeserializeObject: true);
                if (playlist == null || playlist.Count == 0)
                    return OnError(proxyManager);
                #endregion

                file = playlist.First(i => i.id == s).folder.First(i => i.episode == e).folder.First(i => i.title == t).file;
                if (string.IsNullOrWhiteSpace(file))
                    return OnError();

                file = Regex.Replace(file, "^/playlist/", "/");
                file = Regex.Replace(file, "\\.txt$", "");

                urim3u8 = await HttpClient.Post($"https://{vid}.{href}/playlist/{file}.txt", "", timeoutSeconds: 8, proxy: proxy, headers: headers);
                if (urim3u8 == null || !urim3u8.Contains("/index.m3u8"))
                    return OnError(proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, urim3u8, cacheTime(20, init: init));
            }

            if (play)
                return Redirect(HostStreamProxy(init, urim3u8, proxy: proxy));

            return Content("{\"method\":\"play\",\"url\":\"" + HostStreamProxy(init, urim3u8, proxy: proxy) + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region search
        async ValueTask<JArray> search(long kinopoisk_id)
        {
            string memKey = $"hdvb:view:{kinopoisk_id}";

            if (!hybridCache.TryGetValue(memKey, out JArray root))
            {
                var init = AppInit.conf.HDVB;

                root = await HttpClient.Get<JArray>($"{init.host}/api/videos.json?token={init.token}&id_kp={kinopoisk_id}", timeoutSeconds: 8, proxy: proxyManager.Get(), headers: httpHeaders(init));
                if (root == null)
                {
                    proxyManager.Refresh();
                    return null;
                }

                proxyManager.Success();
                hybridCache.Set(memKey, root, cacheTime(40, init: init));
            }

            if (root.Count == 0)
                return null;

            return root;
        }
        #endregion
    }
}
