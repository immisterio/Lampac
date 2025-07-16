using Lampac;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.Module;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Model.Base;
using Shared.Models.CSharpGlobals;
using Shared.Models.Module;
using Shared.PlaywrightCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SISI
{
    public class SisiApiController : BaseController
    {
        #region sisi.js
        [HttpGet]
        [Route("sisi.js")]
        [Route("sisi/js/{token}")]
        public ContentResult Sisi(string token, bool lite)
        {
            if (lite)
                return Content(FileCache.ReadAllText("plugins/sisi.lite.js").Replace("{localhost}", host), "application/javascript; charset=utf-8");

            var init = AppInit.conf.sisi;

            string memKey = $"sisi.js:{init.appReplace?.Count ?? 0}:{init.component}:{init.iconame}:{host}:{init.push_all}:{init.forced_checkRchtype}";
            if (!memoryCache.TryGetValue(memKey, out (string file, string filecleaer) cache))
            {
                cache.file = FileCache.ReadAllText("plugins/sisi.js", saveCache: false);

                if (init.appReplace != null)
                {
                    foreach (var r in init.appReplace)
                    {
                        string val = r.Value;
                        if (val.StartsWith("file:"))
                            val = System.IO.File.ReadAllText(val.AsSpan(5).ToString());

                        cache.file = Regex.Replace(cache.file, r.Key, val, RegexOptions.IgnoreCase);
                    }
                }

                var bulder = new StringBuilder(cache.file);

                if (!init.spider)
                    bulder = bulder.Replace("Lampa.Search.addSource(Search);", "");

                if (init.component != "sisi")
                    bulder = bulder.Replace("'plugin_sisi_'", $"'plugin_{init.component}_'");

                if (!string.IsNullOrEmpty(init.iconame))
                {
                    bulder = bulder.Replace("Defined.use_api == 'pwa'", "true")
                                   .Replace("'<div>p</div>'", $"'<div>{init.iconame}</div>'");
                }

                bulder = bulder.Replace("{localhost}", host)
                               .Replace("{push_all}", init.push_all.ToString().ToLower())
                               .Replace("{token}", HttpUtility.UrlEncode(token));

                if (init.forced_checkRchtype)
                    bulder = bulder.Replace("window.rchtype", "Defined.rchtype");

                cache.file = bulder.ToString();
                cache.filecleaer = cache.file.Replace("{token}", string.Empty);

                if (AppInit.conf.mikrotik == false)
                    memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(1));
            }

            if (!string.IsNullOrEmpty(token))
            {
                if (!string.IsNullOrEmpty(init.eval))
                {
                    string file = CSharpEval.Execute<string>(FileCache.ReadAllText(init.eval), new appReplaceGlobals(cache.file, host, token, requestInfo));
                    return Content(file.Replace("{token}", HttpUtility.UrlEncode(token)), "application/javascript; charset=utf-8");
                }

                return Content(cache.file.Replace("{token}", HttpUtility.UrlEncode(token)), "application/javascript; charset=utf-8");
            }

            if (!string.IsNullOrEmpty(init.eval))
            {
                string file = CSharpEval.Execute<string>(FileCache.ReadAllText(init.eval), new appReplaceGlobals(cache.filecleaer, host, token, requestInfo));
                return Content(file, "application/javascript; charset=utf-8");
            }

            return Content(cache.filecleaer, "application/javascript; charset=utf-8");
        }
        #endregion

        #region modification.js
        [HttpGet]
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
        async public ValueTask<ActionResult> Index(string rchtype, string account_email, string uid, string token, bool spder)
        {
            var conf = AppInit.conf;
            JObject kitconf = await loadKitConf();

            var channels = new List<ChannelItem>(conf.sisi.NextHUB ? 50 : 20) 
            {
                new ChannelItem("Закладки", $"{host}/sisi/bookmarks", 0)
            };

            #region modules
            if (AppInit.modules != null)
            {
                var args = new SisiEventsModel(rchtype, account_email, uid, token);

                foreach (RootModule mod in AppInit.modules.Where(i => i.sisi != null))
                {
                    try
                    {
                        if (mod.assembly.GetType(mod.NamespacePath(mod.sisi)) is Type t)
                        {
                            if (mod.version >= 3)
                            {
                                if (t.GetMethod("Invoke") is MethodInfo m)
                                {
                                    var result = (List<ChannelItem>)m.Invoke(null, new object[] { HttpContext, memoryCache, requestInfo, host, args });
                                    if (result != null && result.Count > 0)
                                        channels.AddRange(result);
                                }

                                if (t.GetMethod("InvokeAsync") is MethodInfo es)
                                {
                                    var result = await (Task<List<ChannelItem>>)es.Invoke(null, new object[] { HttpContext, memoryCache, requestInfo, host, args });
                                    if (result != null && result.Count > 0)
                                        channels.AddRange(result);
                                }
                            }
                            else
                            {
                                if (t.GetMethod("Events") is MethodInfo m)
                                {
                                    var result = (List<ChannelItem>)m.Invoke(null, new object[] { host });
                                    if (result != null && result.Count > 0)
                                        channels.AddRange(result);
                                }
                            }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"Modules {mod.NamespacePath(mod.sisi)}: {ex.Message}\n\n"); }
                }
            }
            #endregion

            #region send
            void send(string name, BaseSettings _init, string plugin = null, string rch_access = null)
            {
                var init = loadKit(_init, kitconf);
                bool enable = init.enable && !init.rip;
                if (!enable)
                    return;

                if (spder == true && init.spider != true)
                    return;

                if (init.rhub && !init.rhub_fallback)
                {
                    if (rch_access != null && rchtype != null)
                    {
                        enable = rch_access.Contains(rchtype);
                        if (enable && init.rhub_geo_disable != null)
                        {
                            if (requestInfo.Country != null && init.rhub_geo_disable.Contains(requestInfo.Country))
                                enable = false;
                        }
                    }
                }

                if (!enable)
                    return;

                if (init.client_type != null && rchtype != null)
                    enable = init.client_type.Contains(rchtype);

                if (init.geo_hide != null)
                {
                    if (requestInfo.Country != null && init.geo_hide.Contains(requestInfo.Country))
                        enable = false;
                }

                if (enable)
                {
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

                    int displayindex = init.displayindex;
                    if (displayindex == 0)
                        displayindex = 20 + channels.Count;

                    channels.Add(new ChannelItem(init.displayname ?? name, url, displayindex));
                }
            }
            #endregion

            #region NextHUB
            if (PlaywrightBrowser.Status != PlaywrightStatus.disabled && conf.sisi.NextHUB)
            {
                foreach (string inFile in Directory.GetFiles("NextHUB", "*.json"))
                {
                    try
                    {
                        if (inFile.Contains(".my."))
                            continue;

                        string plugin = Path.GetFileNameWithoutExtension(inFile);
                        var init = Lampac.Controllers.NextHUB.Root.goInit(plugin);
                        if (init == null)
                            continue;

                        if (init.debug)
                            Console.WriteLine("\n" + JsonConvert.SerializeObject(init, Formatting.Indented));

                        send(Regex.Replace(init.host, "^https?://", ""), init, $"nexthub?plugin={plugin}", init.client_type);
                    }
                    catch (Exception ex) { Console.WriteLine($"NextHUB: error DeserializeObject {inFile}\n {ex.Message}"); }
                }
            }
            #endregion

            send("pornhubpremium.com", conf.PornHubPremium, "phubprem"); // !rhub
            send("pornhub.com", conf.PornHub, "phub", "apk,cors");
            send("xvideos.com", conf.Xvideos, "xds", "apk,cors");
            send("xhamster.com", conf.Xhamster, "xmr", "apk,cors");
            send("ebalovo.porn", conf.Ebalovo, "elo", "apk");
            send("hqporner.com", conf.HQporner, "hqr", "apk,cors");

            if (conf.Spankbang.priorityBrowser == "http" || conf.Spankbang.rhub || PlaywrightBrowser.Status == PlaywrightStatus.NoHeadless || !string.IsNullOrEmpty(conf.Spankbang.overridehost) || conf.Spankbang.overridehosts?.Length > 0)
                send("spankbang.com", conf.Spankbang, "sbg");

            send("eporner.com", conf.Eporner, "epr", "apk,cors");
            send("porntrex.com", conf.Porntrex, "ptx", "apk");
            send("xdsred", conf.XvideosRED, "xdsred");  // !rhub
            send("xnxx.com", conf.Xnxx, "xnx", "apk,cors");
            send("tizam.pw", conf.Tizam, "tizam", "apk,cors");

            if (conf.BongaCams.priorityBrowser == "http" || conf.BongaCams.rhub || PlaywrightBrowser.Status == PlaywrightStatus.NoHeadless || !string.IsNullOrEmpty(conf.BongaCams.overridehost) || conf.BongaCams.overridehosts?.Length > 0)
                send("bongacams.com", conf.BongaCams, "bgs", "apk");

            if (conf.Runetki.priorityBrowser == "http" || conf.Runetki.rhub || PlaywrightBrowser.Status == PlaywrightStatus.NoHeadless || !string.IsNullOrEmpty(conf.Runetki.overridehost) || conf.Runetki.overridehosts?.Length > 0)
                send("runetki.com", conf.Runetki, "runetki", "apk");

            send("chaturbate.com", conf.Chaturbate, "chu", "apk,cors");


            if (conf.sisi.xdb)
            {
                try
                {
                    var ch = await HttpClient.Get<JObject>("https://vi.sisi.am", timeoutSeconds: 4);

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
