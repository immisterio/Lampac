using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Shared.Engine.CORE;
using Online;
using Shared.Model.Templates;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Playwright;
using Shared.Engine;

namespace Lampac.Controllers.LITE
{
    public class Videoseed : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/videoseed")]
        async public Task<ActionResult> Index(long kinopoisk_id, string title, string original_title, int s = -1, bool rjson = false, int serial = -1)
        {
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: false))
                return badInitMsg;

            if (kinopoisk_id == 0 || string.IsNullOrEmpty(init.token))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var proxy = proxyManager.BaseGet();

            #region search
            string memKey = $"videoseed:view:{kinopoisk_id}:{proxyManager.CurrentProxyIp}";
            if (!hybridCache.TryGetValue(memKey, out string location))
            {
                if (serial == 1)
                    return OnError();

                string html = null;
                string uri = $"{init.host}/api.php?list={(serial == 1 ? "serial" : "movie")}&token={init.token}&kp={kinopoisk_id}";
                var root = await HttpClient.Get<JObject>(uri, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxy.proxy);

                if (root == null || !root.ContainsKey("data"))
                {
                    proxyManager.Refresh();
                    return OnError();
                }

                try
                {
                    using (var browser = new Chromium())
                    {
                        var page = await browser.NewPageAsync(proxy: proxy.data);
                        if (page == null)
                            return null;

                        await page.AddInitScriptAsync(@"localStorage.setItem('pljsquality', '1080p');");

                        await page.RouteAsync("**/*", async route =>
                        {
                            if (Regex.IsMatch(route.Request.Url, "/(embed|player)/"))
                            {
                                await route.ContinueAsync();
                                return;
                            }

                            await route.AbortAsync();
                        });

                        var result = await page.GotoAsync(root["data"].First.Value<string>("iframe"));
                        if (result == null)
                            return OnError();

                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        html = await page.ContentAsync();
                    }
                }
                catch
                {
                    return OnError();
                }

                string video = Regex.Match(html ?? "", "<video[^>]+ src=\"([^\"]+)").Groups[1].Value;
                if (string.IsNullOrEmpty(video))
                {
                    proxyManager.Refresh();
                    return OnError();
                }

                proxyManager.Success();
                location = video;
                hybridCache.Set(memKey, location, cacheTime(20, init: init));
            }
            #endregion

            var mtpl = new MovieTpl(title, original_title);
            mtpl.Append(title ?? original_title, HostStreamProxy(init, location, proxy: proxy.proxy), vast: init.vast);

            return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
        }


        #region videoseed_old
        [HttpGet]
        [Route("lite/videoseed_old")]
        async public Task<ActionResult> IndexOld(long kinopoisk_id, string title, string original_title, int s = -1, bool rjson = false, bool origsource = false, int serial = -1)
        {
            return OnError();
            var init = await loadKit(AppInit.conf.Videoseed);
            if (await IsBadInitialization(init, rch: true))
                return badInitMsg;

            if (kinopoisk_id == 0 || string.IsNullOrEmpty(init.token))
                return OnError();

            var proxyManager = new ProxyManager(init);
            var rch = new RchClient(HttpContext, host, init, requestInfo);
            if (rch.IsNotSupport("web", out string rch_error))
                return ShowError(rch_error);

            #region search
            string memKey = rch.ipkey($"videoseed:view:{kinopoisk_id}", proxyManager);
            if (!hybridCache.TryGetValue(memKey, out string html))
            {
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);

                string uri = $"{init.host}/api.php?list={(serial == 1 ? "serial" : "movie")}&token={init.token}&kp={kinopoisk_id}";
                var root = rch.enable ? await rch.Get<JObject>(uri, headers: httpHeaders(init)) :
                                        await HttpClient.Get<JObject>(uri, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxyManager.Get());

                if (root == null || !root.ContainsKey("data"))
                {
                    if (rch.enable)
                        return OnError(null, gbcache: !rch.enable);

                    proxyManager.Refresh();
                    return OnError();
                }

                string iframe = root["data"].First.Value<string>("iframe");

                html = rch.enable ? await rch.Get(iframe, headers: httpHeaders(init)) :
                                    await HttpClient.Get(iframe, timeoutSeconds: 8, headers: httpHeaders(init), proxy: proxyManager.Get());

                if (html == null || !html.Contains("Playerjs"))
                {
                    if (rch.enable)
                        return OnError(null, gbcache: !rch.enable);

                    proxyManager.Refresh();
                    return OnError();
                }

                if (!rch.enable)
                    proxyManager.Success();

                hybridCache.Set(memKey, html, cacheTime(20, init: init));
            }
            #endregion

            if (origsource)
                return Content(html);

            var proxy = proxyManager.Get();

            if (html.Contains("id:\"Video"))
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title);

                var streams = new StreamQualityTpl();
                var match = new Regex("([0-9]+p)\\](\\{[^\\}]+\\} ?)?(?<link>https?://[^,\t\\;\\[ ]+\\.mp4)").Match(html);
                while (match.Success)
                {
                    streams.Insert(HostStreamProxy(init, match.Groups["link"].Value, proxy: proxy), match.Groups[1].Value);
                    match = match.NextMatch();
                }

                if (streams.Any())
                    mtpl.Append(title ?? original_title, streams.Firts().link, streamquality: streams, vast: init.vast);

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                string enc_title = HttpUtility.UrlEncode(title);
                string enc_original_title = HttpUtility.UrlEncode(original_title);

                var root = JsonConvert.DeserializeObject<JArray>(Regex.Match(html, "file:([^\n\r]+)\\}\\);").Groups[1].Value.Trim());

                if (s == -1)
                {
                    var tpl = new SeasonTpl(root.Count);

                    foreach (var season in root)
                    {
                        string name = season.Value<string>("title");
                        if (int.TryParse(Regex.Match(name, "^([0-9]+)").Groups[1].Value, out int _s) && _s > 0)
                        {
                            string link = $"{host}/lite/videoseed?rjson={rjson}&kinopoisk_id={kinopoisk_id}&title={enc_title}&original_title={enc_original_title}&s={_s}";
                            tpl.Append($"{_s} сезон", link, _s);
                        }
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                }
                else
                {
                    var etpl = new EpisodeTpl();

                    foreach (var season in root)
                    {
                        if (!season.Value<string>("title").StartsWith($"{s} "))
                            continue;

                        foreach (var episode in season["folder"])
                        {
                            string name = episode.Value<string>("title");
                            string file = episode.Value<string>("file");

                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(file))
                                continue;

                            var streams = new StreamQualityTpl();

                            var match = new Regex("([0-9]+p)\\](https?://[^,\\;\t\\[ ]+\\.mp4)").Match(file);
                            while (match.Success)
                            {
                                streams.Insert(HostStreamProxy(init, match.Groups[2].Value, proxy: proxy), match.Groups[1].Value);
                                match = match.NextMatch();
                            }

                            if (streams.Any())
                                etpl.Append(name, title ?? original_title, s.ToString(), Regex.Match(name, "([0-9]+)").Groups[1].Value, streams.Firts().link, streamquality: streams, vast: init.vast);
                        }
                    }

                    return ContentTo(rjson ? etpl.ToJson() : etpl.ToHtml());
                }
                #endregion
            }
        }
        #endregion
    }
}
