using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using Lampac.Engine.CORE;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine.CORE;
using Online;

namespace Lampac.Controllers.LITE
{
    public class Lostfilmhd : BaseOnlineController
    {
        ProxyManager proxyManager = new ProxyManager("lostfilmhd", AppInit.conf.Lostfilmhd);

        [HttpGet]
        [Route("lite/lostfilmhd")]
        async public Task<ActionResult> Index(string title, int year, int s = -1)
        {
            if (!AppInit.conf.Lostfilmhd.enable)
                return OnError();

            if (year == 0 || string.IsNullOrWhiteSpace(title))
                return OnError();

            var content = await embed(title, year);
            if (content.seasons == null)
                return OnError(proxyManager);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (s == -1)
            {
                foreach (var season in content.seasons)
                {
                    string link = $"{host}/lite/lostfilmhd?title={HttpUtility.UrlEncode(title)}&year={year}&s={season.Key}";

                    html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
                    firstjson = false;
                }
            }
            else
            {
                if (content.seasons[s.ToString()] is JObject jb)
                {
                    foreach (var episode in jb.ToObject<Dictionary<string, object>>())
                    {
                        string link = $"{host}/lite/lostfilmhd/video?title={HttpUtility.UrlEncode(title)}&iframe={HttpUtility.UrlEncode(content.iframe_src)}&s={s}&e={episode.Key}";
                        string streamlink = $"{link.Replace("/video", "/video.m3u8")}&play=true";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"call\",\"url\":\"" + link + "\",\"stream\":\"" + streamlink + "\",\"title\":\"" + $"{title} ({episode.Key} серия)" + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                        firstjson = false;
                    }
                }
                else
                {
                    return OnError();
                }
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region Video
        [HttpGet]
        [Route("lite/lostfilmhd/video")]
        [Route("lite/lostfilmhd/video.m3u8")]
        async public Task<ActionResult> Video(string iframe, int s, int e, int v, string title, string original_title, bool play)
        {
            var init = AppInit.conf.Lostfilmhd;

            if (!init.enable)
                return OnError();

            var proxy = proxyManager.Get();

            string memKey = $"lostfilmhd:view:{iframe}:{s}:{e}"; // :{v}
            if (!hybridCache.TryGetValue(memKey, out string urim3u8))
            {
                string html = await HttpClient.Get($"{iframe}?season={s}&episode={e}"/* + $"&voice={v}"*/, referer: init.host, proxy: proxy, timeoutSeconds: 10);
                if (html == null)
                    return OnError(proxyManager);

                urim3u8 = new Regex("\"hls\":\"(https?:[^\"]+\\.m3u8)\"").Match(html).Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(urim3u8))
                    return OnError(proxyManager);

                proxyManager.Success();
                hybridCache.Set(memKey, urim3u8, cacheTime(40, init: init));
            }

            if (play)
                return Redirect(HostStreamProxy(init, urim3u8, proxy: proxy));

            return Content("{\"method\":\"play\",\"url\":\"" + HostStreamProxy(init, urim3u8, proxy: proxy) + "\",\"title\":\"" + (title ?? original_title) + "\"}", "application/json; charset=utf-8");
        }
        #endregion

        #region embed
        async ValueTask<(Dictionary<string, object> seasons, string iframe_src)> embed(string title, int year)
        {
            var init = AppInit.conf.Lostfilmhd;
            string memKey = $"lostfilmhd:view:{title}:{year}";

            if (!hybridCache.TryGetValue(memKey, out (Dictionary<string, object> seasons, string iframe_src) cache))
            {
                var proxy = proxyManager.Get();

                string search = await HttpClient.Post($"{init.corsHost()}/publ/", $"query={HttpUtility.UrlEncode(title)}&a=2", timeoutSeconds: 8, proxy: proxy);
                if (search == null)
                    return (null, null);

                string link = null, reservedlink = null;
                foreach (string row in search.Split("<div id=\"entryID").Skip(1))
                {
                    string href = Regex.Match(row, "href=\"/(publ/serialy/[^\"]+)\"").Groups[1].Value;
                    string vent = Regex.Match(row, "<strong>Выпущено</strong>: ([^\n\r<]+)").Groups[1].Value;
                    string eTitle = Regex.Match(row, "class=\"eTitle\"[^>]+><a [^>]+>([^<]+) ([0-9,\\-]+) сезон").Groups[1].Value;

                    if (!string.IsNullOrWhiteSpace(href) && eTitle.ToLower().Trim() == title.ToLower())
                    {
                        reservedlink = href;

                        if (vent.Contains(year.ToString()))
                        {
                            link = href;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                {
                    if (string.IsNullOrWhiteSpace(reservedlink))
                        return (null, null);

                    link = reservedlink;
                }

                string news = await HttpClient.Get($"{init.corsHost()}/{link}", timeoutSeconds: 8, proxy: proxy);
                if (news == null)
                    return (null, null);

                string iframe_src = new Regex("<iframe src=\"//([^/]+/pl/[0-9]+)\"").Match(news).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(iframe_src))
                    return (null, null);

                string iframe = await HttpClient.Get($"http://{iframe_src}", referer: init.host, timeoutSeconds: 8, proxy: proxy);
                if (string.IsNullOrWhiteSpace(iframe) || iframe.Contains(">Контент недоступен в вашем регионе") || iframe.Contains("<title>404 Not Found</title>"))
                    return (null, null);

                string json = new Regex("data-voice=\"[^\"]+\"([^>]+)?>([^<]+)</div>").Match(iframe).Groups[2].Value;
                if (string.IsNullOrWhiteSpace(json))
                    return (null, null);

                cache.iframe_src = $"http://{iframe_src}";
                cache.seasons = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                proxyManager.Success();
                hybridCache.Set(memKey, cache, cacheTime(40, init: init));
            }

            return cache;
        }
        #endregion
    }
}
