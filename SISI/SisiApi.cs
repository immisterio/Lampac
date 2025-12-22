using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Base;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.Module.Entrys;
using Shared.PlaywrightCore;
using System.Text;
using System.Web;
using IO = System.IO;

namespace SISI
{
    public class SisiApiController : BaseController
    {
        #region sisi.js
        [HttpGet]
        [AllowAnonymous]
        [Route("sisi.js")]
        [Route("sisi/js/{token}")]
        public ContentResult Sisi(string token, bool lite)
        {
            if (lite)
                return Content(FileCache.ReadAllText("plugins/sisi.lite.js").Replace("{localhost}", host), "application/javascript; charset=utf-8");

            var init = AppInit.conf.sisi;
            var apr = init.appReplace ?? InvkEvent.conf?.Controller?.AppReplace?.sisi?.regex;

            string memKey = $"sisi.js:{apr?.Count ?? 0}:{init.component}:{init.iconame}:{host}:{init.push_all}:{init.forced_checkRchtype}";
            if (!memoryCache.TryGetValue(memKey, out (string file, string filecleaer) cache))
            {
                cache.file = FileCache.ReadAllText("plugins/sisi.js", saveCache: false)
                    .Replace("{rch_websoket}", FileCache.ReadAllText($"plugins/rch_{AppInit.conf.rch.websoket}.js", saveCache: false));

                #region appReplace
                if (apr != null)
                {
                    foreach (var r in apr)
                    {
                        string val = r.Value;
                        if (val.StartsWith("file:"))
                            val = IO.File.ReadAllText(val.Substring(5));

                        cache.file = Regex.Replace(cache.file, r.Key, val, RegexOptions.IgnoreCase);
                    }
                }

                if (InvkEvent.conf?.Controller?.AppReplace?.sisi?.list != null)
                {
                    foreach (var r in InvkEvent.conf.Controller.AppReplace.sisi.list)
                    {
                        string val = r.Value;
                        if (val.StartsWith("file:"))
                            val = IO.File.ReadAllText(val.Substring(5));

                        cache.file = cache.file.Replace(r.Key, val);
                    }
                }
                #endregion

                var bulder = new StringBuilder(cache.file);

                if (!init.spider)
                    bulder = bulder.Replace("Lampa.Search.addSource(Search);", "");

                if (init.component != "sisi")
                {
                    bulder = bulder.Replace("use_api: 'lampac'", $"use_api: '{init.component}'");
                    bulder = bulder.Replace("'plugin_sisi_'", $"'plugin_{init.component}_'");
                }

                if (!string.IsNullOrEmpty(init.vipcontent))
                    bulder = bulder.Replace("var content = [^\n\r]+", init.vipcontent);

                if (!string.IsNullOrEmpty(init.iconame))
                {
                    bulder = bulder.Replace("Defined.use_api == 'pwa'", "true")
                                   .Replace("'<div>p</div>'", $"'<div>{init.iconame}</div>'");
                }

                bulder = bulder
                    .Replace("{invc-rch}", FileCache.ReadAllText("plugins/invc-rch.js", saveCache: false))
                    .Replace("{invc-rch_nws}", FileCache.ReadAllText("plugins/invc-rch_nws.js", saveCache: false))
                    .Replace("{push_all}", init.push_all.ToString().ToLower())
                    .Replace("{localhost}", host)
                    .Replace("{historySave}", AppInit.conf.sisi.history.enable.ToString().ToLower());

                if (init.forced_checkRchtype)
                    bulder = bulder.Replace("window.rchtype", "Defined.rchtype");

                cache.file = bulder.ToString();
                cache.filecleaer = cache.file.Replace("{token}", string.Empty);

                if (AppInit.conf.mikrotik == false)
                    memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(1));
            }

            if (InvkEvent.conf?.Controller?.AppReplace?.sisi?.eval != null)
            {
                string source = InvkEvent.AppReplace("sisi", new EventAppReplace(cache.file, token, null, host, requestInfo, HttpContext.Request, hybridCache));
                return Content(source.Replace("{token}", HttpUtility.UrlEncode(token)), "application/javascript; charset=utf-8");
            }

            return Content(token != null ? cache.file.Replace("{token}", HttpUtility.UrlEncode(token)) : cache.filecleaer, "application/javascript; charset=utf-8");
        }
        #endregion

        #region modification.js
        [HttpGet]
        [AllowAnonymous]
        [Route("sisi/plugins/modification.js")]
        public ActionResult SisiModification()
        {
            string file = FileCache.ReadAllText("wwwroot/sisi/plugins/modification.js");

            if (!AppInit.conf.sisi.xdb)
                file = file.Replace("addId();", "");

            file = Regex.Replace(file, "\\{localhost\\}/?", $"{host}/sisi");
            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        [Route("sisi")]
        async public ValueTask<JsonResult> Index(string rchtype, string account_email, string uid, string token, bool spder)
        {
            var conf = AppInit.conf;
            JObject kitconf = await loadKitConf();

            bool lgbt = conf.sisi.lgbt;
            if (kitconf != null && kitconf.Value<bool?>("lgbt") == false)
                lgbt = false;

            var channels = new List<ChannelItem>(conf.sisi.NextHUB ? 50 : 20) 
            {
                new ChannelItem("Закладки", $"{host}/sisi/bookmarks", 0)
            };

            if (conf.sisi.history.enable)
                channels.Add(new ChannelItem("История", $"{host}/sisi/historys", 1));

            #region modules
            SisiModuleEntry.EnsureCache();

            if (SisiModuleEntry.sisiModulesCache != null && SisiModuleEntry.sisiModulesCache.Count > 0)
            {
                var args = new SisiEventsModel(rchtype, account_email, uid, token);

                foreach (var entry in SisiModuleEntry.sisiModulesCache)
                {
                    try
                    {
                        if (entry.Invoke != null)
                        {
                            try
                            {
                                var result = entry.Invoke(HttpContext, memoryCache, requestInfo, host, args);
                                if (result != null && result.Count > 0)
                                    channels.AddRange(result);
                            }
                            catch { }
                        }

                        if (entry.InvokeAsync != null)
                        {
                            try
                            {
                                var result = await entry.InvokeAsync(HttpContext, memoryCache, requestInfo, host, args);
                                if (result != null && result.Count > 0)
                                    channels.AddRange(result);
                            }
                            catch { }
                        }

                        if (entry.Events != null)
                        {
                            try
                            {
                                var result = entry.Events(host);
                                if (result != null && result.Count > 0)
                                    channels.AddRange(result);
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"Modules {entry.mod?.NamespacePath(entry.mod.sisi)}: {ex.Message}\n\n"); }
                }
            }
            #endregion

            #region send
            void send(string name, BaseSettings _init, string plugin = null, int displayindex = -1, BaseSettings myinit = null)
            {
                var init = myinit != null ? _init : loadKit(_init, kitconf);
                bool enable = init.enable && !init.rip;
                if (!enable)
                    return;

                if (spder == true && init.spider != true)
                    return;

                if (rchtype != null)
                {
                    if (init.client_type != null && !init.client_type.Contains(rchtype))
                        return;

                    string rch_deny = init.RchAccessNotSupport();
                    if (rch_deny != null && rch_deny.Contains(rchtype))
                        return;

                    string stream_deny = init.StreamAccessNotSupport();
                    if (stream_deny != null && stream_deny.Contains(rchtype))
                        return;

                    if (init.rhub && !init.rhub_fallback && !init.corseu && string.IsNullOrWhiteSpace(init.webcorshost))
                    {
                        if (init.rhub_geo_disable != null &&
                            requestInfo.Country != null &&
                            init.rhub_geo_disable.Contains(requestInfo.Country))
                        {
                            return;
                        }
                    }
                }

                if (init.geo_hide != null &&
                    requestInfo.Country != null && 
                    init.geo_hide.Contains(requestInfo.Country))
                {
                    return;
                }

                if (init.group > 0 && init.group_hide)
                {
                    var user = requestInfo.user;
                    if (user == null || init.group > user.group)
                        return;
                }

                string url = string.Empty;

                if (string.IsNullOrEmpty(init.overridepasswd))
                {
                    url = init.overridehost;
                    if (string.IsNullOrEmpty(url) && init.overridehosts != null && init.overridehosts.Length > 0)
                        url = init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];
                }

                string displayname = init.displayname ?? name;

                if (string.IsNullOrEmpty(url))
                    url = $"{host}/{plugin ?? name.ToLower()}";

                if (displayindex == -1)
                {
                    displayindex = init.displayindex;
                    if (displayindex == 0)
                        displayindex = 20 + channels.Count;
                }

                channels.Add(new ChannelItem(init.displayname ?? name, url, displayindex));
            }
            #endregion

            #region NextHUB
            if (conf.sisi.NextHUB)
            {
                foreach (string inFile in Directory.GetFiles("NextHUB/sites", "*.yaml"))
                {
                    try
                    {
                        if (inFile.Contains(".my."))
                            continue;

                        string plugin = Path.GetFileNameWithoutExtension(inFile);
                        if (!lgbt && plugin == "gayporntube")
                            continue;

                        var init = Controllers.NextHUB.Root.goInit(plugin);
                        if (init == null)
                            continue;

                        if (init.debug)
                            Console.WriteLine("\n" + JsonConvert.SerializeObject(init, Formatting.Indented));

                        init = await loadKit(init);

                        if (PlaywrightBrowser.Status == PlaywrightStatus.disabled || init.rhub)
                        {
                            if (init.priorityBrowser != "http" || (init.view != null && init.view.viewsource == false))
                            {
                                if (AppInit.conf.multiaccess == false)
                                    Console.WriteLine($"NextHUB: {plugin} - Playwright is disabled, skipping.");
                                continue;
                            }
                        }

                        send(Regex.Replace(init.host, "^https?://", ""), init, $"nexthub?plugin={plugin}", myinit: init);
                    }
                    catch (YamlDotNet.Core.YamlException ex)
                    {
                        Console.WriteLine($"\nОшибка: {ex.Message}\nфайл: {Path.GetFileName(inFile)}\nстрока: {ex.Start.Line}");
                    }
                    catch (Exception ex) { Console.WriteLine($"NextHUB: error DeserializeObject {inFile}\n {ex.Message}"); }
                }
            }
            #endregion

            send("pornhubpremium.com", conf.PornHubPremium, "phubprem"); // !rhub
            send("pornhub.com", conf.PornHub, "phub");
            send("xvideos.com", conf.Xvideos, "xds");
            send("xhamster.com", conf.Xhamster, "xmr");
            send("ebalovo.porn", conf.Ebalovo, "elo");
            send("hqporner.com", conf.HQporner, "hqr");

            if (conf.Spankbang.priorityBrowser == "http" || conf.Spankbang.rhub || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Spankbang.overridehost) || conf.Spankbang.overridehosts?.Length > 0)
                send("spankbang.com", conf.Spankbang, "sbg");

            send("eporner.com", conf.Eporner, "epr");
            send("porntrex.com", conf.Porntrex, "ptx");
            send("xdsred", conf.XvideosRED, "xdsred");  // !rhub
            send("xnxx.com", conf.Xnxx, "xnx");
            send("tizam.pw", conf.Tizam, "tizam");

            if (conf.BongaCams.priorityBrowser == "http" || conf.BongaCams.rhub || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.BongaCams.overridehost) || conf.BongaCams.overridehosts?.Length > 0)
                send("bongacams.com", conf.BongaCams, "bgs");

            if (conf.Runetki.priorityBrowser == "http" || conf.Runetki.rhub || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Runetki.overridehost) || conf.Runetki.overridehosts?.Length > 0)
                send("runetki.com", conf.Runetki, "runetki");

            send("chaturbate.com", conf.Chaturbate, "chu");

            if (lgbt)
            {
                send("phubgay", conf.PornHub, "phubgay", 10_100);
                send("phubtrans", conf.PornHub, "phubsml", 10_101);
                send("xdsgay", conf.Xvideos, "xdsgay", 10_102);
                send("xdstrans", conf.Xvideos, "xdssml", 10_103);
                send("xmrgay", conf.Xhamster, "xmrgay", 10_104);
                send("xmrtrans", conf.Xhamster, "xmrsml", 10_105);
            }

            if (conf.sisi.xdb)
            {
                try
                {
                    var ch = await Http.Get<JObject>("https://vi.sisi.am", timeoutSeconds: 4);

                    foreach (var pl in ch.GetValue("channels"))
                    {
                        string title = pl.Value<string>("title").Replace("pornhubpremium.com", "phubprem.com");
                        string playlist_url = pl.Value<string>("playlist_url");

                        if (playlist_url.Contains("/bookmarks"))
                            continue;

                        if (channels.FirstOrDefault(i => i.title == title).title != null)
                            continue;

                        channels.Add(new ChannelItem(title, playlist_url, 20 + channels.Count));
                    }
                }
                catch { }
            }

            return Json(new
            {
                title = "sisi",
                channels = channels.OrderBy(i => i.displayindex)
            });
        }
    }
}
