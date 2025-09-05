using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Models.Online.Settings;
using Shared.PlaywrightCore;
using System.Text;

namespace Online.Controllers
{
    public class AutoEmbed : BaseENGController
    {
        [HttpGet]
        [Route("lite/autoembed")]
        public ValueTask<ActionResult> Index(bool checksearch, long id, string imdb_id, string title, string original_title, int serial, int s = -1, bool rjson = false)
        {
            var init = AppInit.conf.Autoembed;
            return ViewTmdb(init, checksearch, id, imdb_id, title, original_title, serial, s, rjson, mp4: true, method: "call");
        }


        #region Video
        [HttpGet]
        [Route("lite/autoembed/video")]
        async public ValueTask<ActionResult> Video(long id, int s = -1, int e = -1, bool play = false)
        {
            var init = await loadKit(AppInit.conf.Autoembed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (init.priorityBrowser != "http" && Firefox.Status == PlaywrightStatus.disabled)
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            string embed = $"{init.host}/embed/movie/{id}?server=1";
            if (s > 0)
                embed = $"{init.host}/embed/tv/{id}/{s}/{e}?server=1";

            return await InvkSemaphore(init, embed, async () =>
            {
                if (init.priorityBrowser == "http")
                {
                    string apihost = "https://nono.autoembed.cc";
                    string uri = $"{apihost}/api/getVideoSource?type=movie&id={id}";
                    if (s > 0)
                        uri = $"{apihost}/api/getVideoSource?type=tv&id={id}%2F{s}%2F{e}";

                    if (!hybridCache.TryGetValue(uri, out JObject data))
                    {
                        var root = await Http.Get<JObject>(uri, timeoutSeconds: 8, proxy: proxy.proxy, headers: HeadersModel.Init(
                            ("Accept-Language", "en-US,en;q=0.5"),
                            ("Alt-Used", "nono.autoembed.cc"),
                            ("Priority", "u=4"),
                            ("Referer", $"{apihost}/tv/{id}/{s}/{e}"),
                            ("Sec-Fetch-Dest", "empty"),
                            ("Sec-Fetch-Mode", "cors"),
                            ("Sec-Fetch-Site", "same-origin"),
                            ("TE", "trailers"),
                            ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0")
                        ));

                        if (root == null && !root.ContainsKey("encryptedData"))
                        {
                            proxyManager.Refresh();
                            return OnError();
                        }

                        string encryptedData = root.Value<string>("encryptedData");
                        if (string.IsNullOrEmpty(encryptedData))
                            return OnError();

                        var postdata = new System.Net.Http.StringContent("{\"encryptedData\":\"" + encryptedData + "\"}", Encoding.UTF8, "application/json");
                        var videoSource = await Http.Post<JObject>($"{apihost}/api/decryptVideoSource", postdata, timeoutSeconds: 8, proxy: proxy.proxy, headers: HeadersModel.Init(
                            ("Accept-Language", "en-US,en;q=0.5"),
                            ("Alt-Used", "nono.autoembed.cc"),
                            ("Origin", apihost),
                            ("Priority", "u=4"),
                            ("Referer", $"{apihost}/tv/{id}/{s}/{e}"),
                            ("Sec-Fetch-Dest", "empty"),
                            ("Sec-Fetch-Mode", "cors"),
                            ("Sec-Fetch-Site", "same-origin"),
                            ("TE", "trailers"),
                            ("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:135.0) Gecko/20100101 Firefox/135.0")
                        ));

                        if (videoSource == null && !root.ContainsKey("videoSource"))
                            return OnError();

                        data = videoSource;
                        proxyManager.Success();
                        hybridCache.Set(uri, data, cacheTime(20));
                    }

                    var subtitles = new SubtitleTpl();
                    try
                    {
                        foreach (var sub in data["subtitles"])
                            subtitles.Append(sub.Value<string>("label"), HostStreamProxy(init, sub.Value<string>("file"), proxy: proxy.proxy));
                    }
                    catch { }

                    string file = HostStreamProxy(init, data.Value<string>("videoSource"), proxy: proxy.proxy);
                    if (play)
                        return Redirect(file);

                    return ContentTo(VideoTpl.ToJson("play", file, "English", subtitles: subtitles, vast: init.vast, headers: httpHeaders(init.host, init.headers_stream)));
                }
                else
                {
                    string file = await black_magic(embed, init, proxyManager, proxy.data);
                    if (file == null)
                        return StatusCode(502);

                    file = HostStreamProxy(init, file, proxy: proxy.proxy);

                    if (play)
                        return Redirect(file);

                    return ContentTo(VideoTpl.ToJson("play", file, "English", vast: init.vast, headers: httpHeaders(init.host, init.headers_stream)));
                }
            });
        }
        #endregion

        #region black_magic
        async ValueTask<string> black_magic(string uri, OnlinesSettings init, ProxyManager proxyManager, (string ip, string username, string password) proxy)
        {
            if (string.IsNullOrEmpty(uri))
                return uri;

            try
            {
                string memKey = $"autoembed:black_magic:{uri}";
                if (!hybridCache.TryGetValue(memKey, out string mp4))
                {
                    using (var browser = new Firefox())
                    {
                        var page = await browser.NewPageAsync(init.plugin, httpHeaders(init).ToDictionary(), proxy);
                        if (page == null)
                            return null;

                        await page.RouteAsync("**/*", async route =>
                        {
                            try
                            {
                                if (await PlaywrightBase.AbortOrCache(page, route, abortMedia: true, fullCacheJS: true))
                                    return;

                                if (browser.IsCompleted || Regex.IsMatch(route.Request.Url, "(/ads/|vast.xml|ping.gif|fonts.googleapis\\.)"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: Abort {route.Request.Url}");
                                    await route.AbortAsync();
                                    return;
                                }

                                if (/*route.Request.Url.Contains("hakunaymatata.") &&*/ route.Request.Url.Contains(".mp4"))
                                {
                                    PlaywrightBase.ConsoleLog($"Playwright: SET {route.Request.Url}");
                                    browser.IsCompleted = true;
                                    browser.completionSource.SetResult(route.Request.Url);
                                    await route.AbortAsync();
                                    return;
                                }

                                await route.ContinueAsync();
                            }
                            catch { }
                        });

                        PlaywrightBase.GotoAsync(page, uri);
                        mp4 = await browser.WaitPageResult();
                    }

                    if (mp4 == null)
                    {
                        proxyManager.Refresh();
                        return null;
                    }

                    proxyManager.Success();
                    hybridCache.Set(memKey, mp4, cacheTime(20, init: init));
                }

                return mp4;
            }
            catch { return null; }
        }
        #endregion
    }
}
