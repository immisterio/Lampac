using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.Module;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Model.Base;

namespace SISI
{
    public class SisiApiController : BaseController
    {
        #region sisi.js
        [HttpGet]
        [Route("sisi.js")]
        [Route("sisi/js/{token}")]
        public ActionResult Sisi(string token, bool lite)
        {
            if (lite)
                return Content(FileCache.ReadAllText("plugins/sisi.lite.js").Replace("{localhost}", host), contentType: "application/javascript; charset=utf-8");

            var init = AppInit.conf.sisi;
            string file = FileCache.ReadAllText("plugins/sisi.js").Replace("{localhost}", host);

            if (init.component != "sisi")
                file = file.Replace("'plugin_sisi_'", $"'plugin_{init.component}_'");

            if (!string.IsNullOrEmpty(init.iconame))
            {
                file = file.Replace("Defined.use_api == 'pwa'", "true");
                file = file.Replace("'<div>p</div>'", $"'<div>{init.iconame}</div>'");
            }

            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
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
        async public Task<ActionResult> Index(string rchtype)
        {
            var conf = AppInit.conf;
            var channels = new List<ChannelItem>() 
            {
                new ChannelItem("Закладки", $"{host}/sisi/bookmarks")
            };

            #region modules
            if (AppInit.modules != null)
            {
                foreach (RootModule mod in AppInit.modules.Where(i => i.sisi != null))
                {
                    try
                    {
                        if (mod.assembly.GetType(mod.sisi) is Type t && t.GetMethod("Events") is MethodInfo m)
                        {
                            var result = (List<ChannelItem>)m.Invoke(null, new object[] { host });
                            if (result != null && result.Count > 0)
                                channels.AddRange(result);
                        }
                    }
                    catch { }
                }
            }
            #endregion

            #region send
            void send(string name, BaseSettings init, string plugin = null, string rch_access = null)
            {
                bool enable = init.enable && !init.rip;

                if (enable && init.rhub && !init.rhub_fallback)
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

                if (init.geo_hide != null)
                {
                    if (requestInfo.Country != null && init.geo_hide.Contains(requestInfo.Country))
                        enable = false;
                }

                if (enable)
                {
                    if (AppInit.conf.accsdb.enable)
                    {
                        var user = requestInfo.user;
                        if (user == null || (init.group > user.group && init.group_hide))
                            return;
                    }

                    string url = init.overridehost;
                    if (string.IsNullOrEmpty(url) && init.overridehosts != null && init.overridehosts.Length > 0)
                        url = init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];

                    string displayname = init.displayname ?? name;

                    if (string.IsNullOrEmpty(url))
                        url = $"{host}/{plugin ?? name.ToLower()}";

                    channels.Add(new ChannelItem(init.displayname ?? name, url));
                }
            }
            #endregion


            send("pornhubpremium.com", conf.PornHubPremium, "phubprem");
            send("pornhub.com", conf.PornHub, "phub");
            send("xvideos.com", conf.Xvideos, "xds");
            send("xhamster.com", conf.Xhamster, "xmr");
            send("ebalovo.porn", conf.Ebalovo, "elo");
            send("hqporner.com", conf.HQporner, "hqr", "apk,cors");
            send("spankbang.com", conf.Spankbang, "sbg");
            send("eporner.com", conf.Eporner, "epr");
            send("porntrex.com", conf.Porntrex, "ptx");
            send("xdsred", conf.XvideosRED, "xdsred");
            send("xnxx.com", conf.Xnxx, "xnx");
            send("tizam.pw", conf.Tizam, "tizam");
            send("bongacams.com", conf.BongaCams, "bgs", "apk,cors");
            send("chaturbate.com", conf.Chaturbate, "chu");


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

                        if (channels.FirstOrDefault(i => i.title == title) != null)
                            continue;

                        channels.Add(new ChannelItem(title, playlist_url));
                    }
                }
                catch { }
            }

            return Json(new
            {
                title = "sisi",
                channels
            });
        }
    }
}
