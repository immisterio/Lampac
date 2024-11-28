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
                return Content(FileCache.ReadAllText("plugins/sisi.lite.js").Replace("{localhost}", $"{host}/sisi"), contentType: "application/javascript; charset=utf-8");

            var init = AppInit.conf.sisi;
            string file = FileCache.ReadAllText("plugins/sisi.js").Replace("{localhost}", $"{host}/sisi");

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
        async public Task<ActionResult> Index()
        {
            var conf = AppInit.conf;
            var channels = new List<ChannelItem>() 
            {
                new ChannelItem("Закладки", $"{host}/sisi/bookmarks")
            };

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

            string gourl(BaseSettings init, string @default)
            {
                string url = init.overridehost;
                if (string.IsNullOrEmpty(url) && init.overridehosts != null && init.overridehosts.Length > 0)
                    return init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];
                else 
                    return @default;
            }

            if (conf.PornHubPremium.enable)
                channels.Add(new ChannelItem("pornhubpremium.com", gourl(conf.PornHubPremium, $"{host}/phubprem")));

            if (conf.PornHub.enable)
                channels.Add(new ChannelItem("pornhub.com", gourl(conf.PornHub, $"{host}/phub")));

            if (conf.Xvideos.enable)
                channels.Add(new ChannelItem("xvideos.com", gourl(conf.Xvideos, $"{host}/xds")));

            if (conf.Xhamster.enable)
                channels.Add(new ChannelItem("xhamster.com", gourl(conf.Xhamster, $"{host}/xmr")));

            if (conf.Ebalovo.enable)
                channels.Add(new ChannelItem("ebalovo.porn", gourl(conf.Ebalovo, $"{host}/elo")));

            if (conf.HQporner.enable)
                channels.Add(new ChannelItem("hqporner.com", gourl(conf.HQporner, $"{host}/hqr")));

            if (conf.Spankbang.enable)
                channels.Add(new ChannelItem("spankbang.com", gourl(conf.Spankbang, $"{host}/sbg")));

            if (conf.Eporner.enable)
                channels.Add(new ChannelItem("eporner.com", gourl(conf.Eporner, $"{host}/epr")));

            if (conf.Porntrex.enable)
                channels.Add(new ChannelItem("porntrex.com", gourl(conf.Porntrex, $"{host}/ptx")));

            if (conf.XvideosRED.enable)
                channels.Add(new ChannelItem("xdsred", gourl(conf.XvideosRED, $"{host}/xdsred")));

            if (conf.Xnxx.enable)
                channels.Add(new ChannelItem("xnxx.com", gourl(conf.Xnxx, $"{host}/xnx")));

            if (conf.Tizam.enable)
                channels.Add(new ChannelItem("tizam.pw", gourl(conf.Tizam, $"{host}/tizam")));

            if (conf.BongaCams.enable)
                channels.Add(new ChannelItem("bongacams.com", gourl(conf.BongaCams, $"{host}/bgs")));

            if (conf.Chaturbate.enable)
                channels.Add(new ChannelItem("chaturbate.com", gourl(conf.Chaturbate, $"{host}/chu")));

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
