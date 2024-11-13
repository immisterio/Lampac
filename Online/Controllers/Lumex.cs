using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.Online;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using Shared.Engine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Model.Online.Lumex;
using Shared.Model.Online;
using Microsoft.Extensions.Caching.Memory;
using System;

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

            if (IsOverridehost(init, out string overridehost))
                return Redirect(overridehost);

            string log = $"{HttpContext.Request.Path.Value}\n\nstart init\n";

            var proxyManager = new ProxyManager("lumex", init);
            var proxy = proxyManager.Get();

            var oninvk = new LumexInvoke
            (
               init,
               (url, referer) => HttpClient.Get(init.cors(url), referer: referer, timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(init)),
               streamfile => HostStreamProxy(init, streamfile, proxy: proxy, plugin: "lumex"),
               host,
               requesterror: () => proxyManager.Refresh()
            );

            if (kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
            {
                var search = await InvokeCache<SimilarTpl>($"lumex:search:{title}:{original_title}", cacheTime(40, init: init), async res =>
                {
                    return await oninvk.Search(title, original_title, serial);
                });

                return OnResult(search, () => rjson ? search.Value.ToJson() : search.Value.ToHtml());
            }

            var cache = await InvokeCache<EmbedModel>($"videocdn:{imdb_id}:{kinopoisk_id}", cacheTime(20, init: init), proxyManager,  async res =>
            {
                try
                {
                    using (var browser = await PuppeteerTo.Browser())
                    {
                        if (browser == null)
                            return null;

                        log += "browser init\n";

                        var page = await browser.Page(new Dictionary<string, string>()
                        {
                            ["referer"] = "https://ikino.org/37521-odinokie-volki-2024.html"
                        });

                        if (page == null)
                            return null;

                        string content = null, csrf = null;
                        await page.SetRequestInterceptionAsync(true);

                        page.Request += async (sender, e) =>
                        {
                            try
                            {
                                if (e?.Request == null)
                                    return;

                                if (!string.IsNullOrEmpty(content))
                                {
                                    await e.Request.AbortAsync();
                                }
                                else if (e.Request.Method.Method != "GET" || e.Request.Url.Contains("/validate/") || Regex.IsMatch(e.Request.Url, "\\.(woff|jpe?g|png|ico)", RegexOptions.IgnoreCase))
                                {
                                    await e.Request.AbortAsync();
                                }
                                else
                                {
                                    if (Regex.IsMatch(e.Request.Url, "(gstatic|lumex)\\.", RegexOptions.IgnoreCase))
                                        await e.Request.ContinueAsync();
                                    else
                                        await e.Request.AbortAsync();
                                }
                            }
                            catch { }
                        };

                        page.Response += async (sender, e) =>
                        {
                            try
                            {
                                if (e?.Response != null && string.IsNullOrEmpty(content))
                                {
                                    log += $"browser Response.Url / {e.Response?.Url}\n";

                                    if (!string.IsNullOrEmpty(e.Response.Url) && e.Response.Url.Contains("contentId=") && e.Response.Url.Contains("api.lumex"))
                                    {
                                        content = await e.Response.TextAsync();
                                        csrf = Regex.Match(e.Response.Headers["set-cookie"], "x-csrf-token=([^;]+)").Groups[1].Value;
                                    }
                                }
                            }
                            catch { }
                        };

                        string args = kinopoisk_id > 0 ? $"kp_id={kinopoisk_id}&imdb_id={imdb_id}" : $"imdb_id={imdb_id}";

                        log += $"browser GoToAsync / {init.corsHost()}?{args}\n";
                        await page.GoToAsync($"{init.corsHost()}?{args}");

                        for (int i = 0; i < 100; i++)
                        {
                            if (content != null)
                                break;

                            await Task.Delay(50);
                        }

                        if (string.IsNullOrEmpty(csrf) || string.IsNullOrEmpty(content))
                        {
                            log += $"\ncsrf || content == null\n\ncsrf: {csrf}\n\ncontent: {content}\n";
                            return null;
                        }

                        log += $"\ncsrf: {csrf}\n\ncontent: {content}\n";
                        var md = JsonConvert.DeserializeObject<JObject>(content)["player"].ToObject<EmbedModel>();
                        md.csrf = csrf;

                        return md;
                    }
                }
                catch (Exception ex) { log += $"\nex: {ex}\n"; }

                return null;
            });

            OnLog(log + "\nStart OnResult");

            return OnResult(cache, () => oninvk.Html(cache.Value, imdb_id, kinopoisk_id, title, original_title, t, s, rjson: rjson), origsource: origsource);
        }


        #region Video
        [HttpGet]
        [Route("lite/lumex/video.m3u8")]
        async public Task<ActionResult> Video(string playlist, string csrf)
        {
            var init = AppInit.conf.Lumex;
            if (!init.enable)
                return OnError("disable");

            string memkey = $"lumex/video:{playlist}:{csrf}";
            if (!memoryCache.TryGetValue(memkey, out string location))
            {
                var result = await HttpClient.Post<JObject>("https://api.lumex.pw" + playlist, "", headers: HeadersModel.Init(
                    ("accept", "*/*"),
                    ("accept-language", "ru-RU,ru;q=0.9,uk-UA;q=0.8,uk;q=0.7,en-US;q=0.6,en;q=0.5"),
                    ("cache-control", "no-cache"),
                    ("cookie", $"x-csrf-token={csrf}"),
                    ("dnt", "1"),
                    ("origin", "https://p.lumex.pw"),
                    ("pragma", "no-cache"),
                    ("priority", "u=1, i"),
                    ("referer", "https://p.lumex.pw/"),
                    ("sec-ch-ua", "\"Chromium\";v=\"130\", \"Google Chrome\";v=\"130\", \"Not?A_Brand\";v=\"99\""),
                    ("sec-ch-ua-mobile", "?0"),
                    ("sec-ch-ua-platform", "\"Windows\""),
                    ("sec-fetch-dest", "empty"),
                    ("sec-fetch-mode", "cors"),
                    ("sec-fetch-site", "same-site"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36"),
                    ("x-csrf-token", csrf.Split("%")[0])
                ));

                if (result == null || !result.ContainsKey("url"))
                    return OnError();

                string url = result.Value<string>("url");
                if (string.IsNullOrEmpty(url))
                    return OnError();

                location = $"http:{url}";
                memoryCache.Set(memkey, location, cacheTime(20, init: init));
            }

            var proxyManager = new ProxyManager("lumex", init);

            return Redirect(HostStreamProxy(init, location, proxy: proxyManager.Get(), plugin: "lumex"));
        }
        #endregion
    }
}
