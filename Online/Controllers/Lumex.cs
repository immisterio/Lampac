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
        async public Task<ActionResult> Index(string imdb_id, long kinopoisk_id, string title, string original_title, string t, int clarification, int s = -1, int serial = -1, bool origsource = false, bool rjson = false)
        {
            var init = await loadKit(AppInit.conf.Lumex);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            string log = $"{HttpContext.Request.Path.Value}\n\nstart init\n";

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            var oninvk = new LumexInvoke
            (
               init,
               (url, referer) => HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => streamfile,
               host,
               requesterror: () => proxyManager.Refresh()
            );

            if (clarification == 1 || (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
            {
                var search = await InvokeCache<SimilarTpl>($"lumex:search:{title}:{original_title}:{clarification}", cacheTime(40, init: init), async res =>
                {
                    return await oninvk.Search(title, original_title, serial, clarification);
                });

                return OnResult(search, () => rjson ? search.Value.ToJson() : search.Value.ToHtml());
            }

            var cache = await InvokeCache<EmbedModel>($"videocdn:{kinopoisk_id}:{imdb_id}", cacheTime(10, init: init), proxyManager,  async res =>
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

                string args = "";
                if (!string.IsNullOrEmpty(imdb_id))
                    args += $"&imdbId={imdb_id}";
                if (kinopoisk_id > 0)
                    args += $"&kpId={kinopoisk_id}";

                var result = await HttpClient.BaseGetAsync($"https://api.{init.iframehost}/content?clientId={init.clientId}&contentType=short"+args+init.args_api, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init));

                if (string.IsNullOrEmpty(result.content))
                {
                    proxyManager.Refresh();
                    return res.Fail("content");
                }

                if (!result.response.Headers.TryGetValues("Set-Cookie", out var cook))
                {
                    proxyManager.Refresh();
                    return res.Fail("cook");
                }

                string csrf = Regex.Match(cook.FirstOrDefault() ?? "", "x-csrf-token=([^\n\r; ]+)").Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(csrf))
                {
                    proxyManager.Refresh();
                    return res.Fail("csrf");
                }

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
            var init = await loadKit(AppInit.conf.Lumex);
            if (await IsBadInitialization(init))
                return badInitMsg;

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.Get();

            string memkey = $"lumex/video:{playlist}:{csrf}";
            if (!memoryCache.TryGetValue(memkey, out string hls))
            {
                var result = await HttpClient.Post<JObject>($"https://api.{init.iframehost}" + playlist, "", proxy: proxy, timeoutSeconds: 8, headers: httpHeaders(init, HeadersModel.Init(
                    ("cookie", $"x-csrf-token={csrf}"),
                    ("x-csrf-token", csrf.Split("%")[0])
                )));

                if (result == null || !result.ContainsKey("url"))
                    return OnError();

                string url = result.Value<string>("url");
                if (string.IsNullOrEmpty(url))
                    return OnError();

                hls = $"{init.scheme}:{url}";
                memoryCache.Set(memkey, hls, cacheTime(20, init: init));
            }

            string sproxy(string uri) => HostStreamProxy(init, uri, proxy: proxy);

            if (max_quality > 0 && !init.hls)
            {
                var streams = new List<(string quality, string link)>(5);
                foreach (int q in new int[] { 1080, 720, 480, 360, 240 })
                {
                    if (max_quality >= q)
                        streams.Add(($"{q}p", sproxy(Regex.Replace(hls, "/hls\\.m3u8$", $"/{q}.mp4"))));
                }

                return ContentTo(VideoTpl.ToJson("play", streams[0].link, streams[0].quality, streamquality: new StreamQualityTpl(streams), vast: init.vast));
            }

            return Redirect(sproxy(hls));
        }
        #endregion
    }
}
