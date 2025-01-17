using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Model.Online.Lumex;
using Shared.Model.Online;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class Lumex : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/lumex")]
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int s = -1, int serial = -1, bool origsource = false, bool rjson = false)
        {
            var init = AppInit.conf.Lumex;
            if (!init.enable)
                return OnError();

            if (init.rhub)
                return ShowError(RchClient.ErrorMsg);

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            string log = $"{HttpContext.Request.Path.Value}\n\nstart init\n";

            var proxyManager = new ProxyManager("lumex", init);
            var proxy = proxyManager.Get();

            var oninvk = new LumexInvoke
            (
               init,
               (url, referer) => HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => streamfile,
               host,
               requesterror: () => proxyManager.Refresh()
            );

            if (kinopoisk_id == 0 /*&& string.IsNullOrEmpty(imdb_id)*/)
            {
                var search = await InvokeCache<SimilarTpl>($"lumex:search:{title}:{original_title}", cacheTime(40, init: init), async res =>
                {
                    return await oninvk.Search(title, original_title, serial);
                });

                return OnResult(search, () => rjson ? search.Value.ToJson() : search.Value.ToHtml());
            }

            var cache = await InvokeCache<EmbedModel>($"videocdn:{imdb_id}:{kinopoisk_id}", cacheTime(20, init: init), proxyManager,  async res =>
            {
                #region chromium
                //try
                //{
                //    using (var browser = await PuppeteerTo.Browser())
                //    {
                //        if (browser == null)
                //            return null;

                //        log += "browser init\n";

                //        var page = await browser.Page(new Dictionary<string, string>()
                //        {
                //            ["referer"] = "https://ikino.org/37521-odinokie-volki-2024.html"
                //        });

                //        if (page == null)
                //            return null;

                //        string content = null, csrf = null;
                //        await page.SetRequestInterceptionAsync(true);

                //        page.Request += async (sender, e) =>
                //        {
                //            try
                //            {
                //                if (e?.Request == null)
                //                    return;

                //                if (!string.IsNullOrEmpty(content))
                //                {
                //                    await e.Request.AbortAsync();
                //                }
                //                else if (e.Request.Method.Method != "GET" || e.Request.Url.Contains("/validate/") || Regex.IsMatch(e.Request.Url, "\\.(woff|jpe?g|png|ico)", RegexOptions.IgnoreCase))
                //                {
                //                    await e.Request.AbortAsync();
                //                }
                //                else
                //                {
                //                    if (Regex.IsMatch(e.Request.Url, "(gstatic|lumex)\\.", RegexOptions.IgnoreCase))
                //                        await e.Request.ContinueAsync();
                //                    else
                //                        await e.Request.AbortAsync();
                //                }
                //            }
                //            catch { }
                //        };

                //        page.Response += async (sender, e) =>
                //        {
                //            try
                //            {
                //                if (e?.Response != null && string.IsNullOrEmpty(content))
                //                {
                //                    log += $"browser Response.Url / {e.Response?.Url}\n";

                //                    if (!string.IsNullOrEmpty(e.Response.Url) && e.Response.Url.Contains("contentId=") && e.Response.Url.Contains("api.lumex"))
                //                    {
                //                        content = await e.Response.TextAsync();
                //                        csrf = Regex.Match(e.Response.Headers["set-cookie"], "x-csrf-token=([^;]+)").Groups[1].Value;
                //                    }
                //                }
                //            }
                //            catch { }
                //        };

                //        string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";

                //        log += $"browser GoToAsync / {init.corsHost()}?{args}\n";
                //        await page.GoToAsync($"{init.corsHost()}?{args}");

                //        for (int i = 0; i < 100; i++)
                //        {
                //            if (content != null)
                //                break;

                //            await Task.Delay(50);
                //        }

                //        if (string.IsNullOrEmpty(csrf) || string.IsNullOrEmpty(content))
                //        {
                //            log += $"\ncsrf || content == null\n\ncsrf: {csrf}\n\ncontent: {content}\n";
                //            return null;
                //        }

                //        log += $"\ncsrf: {csrf}\n\ncontent: {content}\n";
                //        var md = JsonConvert.DeserializeObject<JObject>(content)["player"].ToObject<EmbedModel>();
                //        md.csrf = csrf;

                //        return md;
                //    }
                //}
                //catch (Exception ex) { log += $"\nex: {ex}\n"; return null; }
                #endregion

                var result = await HttpClient.BaseGetAsync($"https://api.{init.iframehost}/content?clientId={init.clientId}&contentType=short&kpId={kinopoisk_id}", timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init, HeadersModel.Init(
                    ("Accept", "*/*"),
                    ("Origin", $"https://p.{init.iframehost}"),
                    ("Referer", $"https://p.{init.iframehost}/"),
                    ("Sec-Ch-Ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                    ("Sec-Ch-Ua-Mobile", "?0"),
                    ("Sec-Ch-Ua-Platform", "\"Windows\""),
                    ("Sec-Fetch-Dest", "empty"),
                    ("Sec-Fetch-Mode", "cors"),
                    ("Sec-Fetch-Site", "same-site"),
                    ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36")
                )));

                if (string.IsNullOrEmpty(result.content))
                    return OnError(proxyManager);

                if (!result.response.Headers.TryGetValues("Set-Cookie", out var cook))
                    return OnError(proxyManager);

                string csrf = Regex.Match(cook.FirstOrDefault() ?? "", "x-csrf-token=([^\n\r; ]+)").Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(csrf))
                    return OnError(proxyManager);

                var md = JsonConvert.DeserializeObject<JObject>(result.content)["player"].ToObject<EmbedModel>();
                md.csrf = csrf;

                return md;
            });

            OnLog(log + "\nStart OnResult");

            return OnResult(cache, () => oninvk.Html(cache.Value, accsArgs(string.Empty), imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson), origsource: origsource);
        }


        #region Video
        [HttpGet]
        [Route("lite/lumex/video")]
        [Route("lite/lumex/video.m3u8")]
        async public Task<ActionResult> Video(string playlist, string csrf, int max_quality)
        {
            var init = AppInit.conf.Lumex;
            if (!init.enable)
                return OnError("disable");

            if (NoAccessGroup(init, out string error_msg))
                return ShowError(error_msg);

            var proxyManager = new ProxyManager("lumex", init);
            var proxy = proxyManager.Get();

            string memkey = $"lumex/video:{playlist}:{csrf}";
            if (!memoryCache.TryGetValue(memkey, out string hls))
            {
                var result = await HttpClient.Post<JObject>($"https://api.{init.iframehost}" + playlist, "", proxy: proxy, timeoutSeconds: 8, headers: HeadersModel.Init(
                    ("accept", "*/*"),
                    ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                    ("cache-control", "no-cache"),
                    ("cookie", $"x-csrf-token={csrf}"),
                    ("dnt", "1"),
                    ("Origin", $"https://p.{init.iframehost}"),
                    ("Referer", $"https://p.{init.iframehost}/"),
                    ("pragma", "no-cache"),
                    ("priority", "u=1, i"),
                    ("Sec-Ch-Ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-site"),
                    ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"),
                    ("x-csrf-token", csrf.Split("%")[0])
                ));

                if (result == null || !result.ContainsKey("url"))
                    return OnError();

                string url = result.Value<string>("url");
                if (string.IsNullOrEmpty(url))
                    return OnError();

                hls = $"{init.scheme}:{url}";
                memoryCache.Set(memkey, hls, cacheTime(20, init: init));
            }

            string sproxy(string uri) => HostStreamProxy(init, uri, proxy: proxy, plugin: "lumex");

            if (max_quality > 0 && !init.hls)
            {
                var streams = new List<(string quality, string link)>(5);
                foreach (int q in new int[] { 1080, 720, 480, 360, 240 })
                {
                    if (max_quality >= q)
                        streams.Add(($"{q}p", Regex.Replace(hls, "/hls\\.m3u8$", $"/{q}.mp4")));
                }

                return ContentTo(VideoTpl.ToJson("play", streams[0].link, streams[0].quality, streamquality: new StreamQualityTpl(streams), vast: init.vast));
            }

            return Redirect(sproxy(hls));
        }
        #endregion
    }
}
