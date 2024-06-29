using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.Module;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Shared.Engine;

namespace SISI
{
    public class SisiApiController : BaseController
    {
        #region sisi.js
        [HttpGet]
        [Route("sisi.js")]
        public ActionResult Sisi(bool lite)
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

            if (conf.PornHubPremium.enable)
                channels.Add(new ChannelItem("pornhubpremium.com", conf.PornHubPremium.overridehost ?? $"{host}/phubprem"));

            if (conf.PornHub.enable)
                channels.Add(new ChannelItem("pornhub.com", conf.PornHub.overridehost ?? $"{host}/phub"));

            if (conf.Xvideos.enable)
                channels.Add(new ChannelItem("xvideos.com", conf.Xvideos.overridehost ?? $"{host}/xds"));

            if (conf.Xhamster.enable)
                channels.Add(new ChannelItem("xhamster.com", conf.Xhamster.overridehost ?? $"{host}/xmr"));

            if (conf.Ebalovo.enable)
                channels.Add(new ChannelItem("ebalovo.porn", conf.Ebalovo.overridehost ?? $"{host}/elo"));

            if (conf.HQporner.enable)
                channels.Add(new ChannelItem("hqporner.com", conf.HQporner.overridehost ?? $"{host}/hqr"));

            if (conf.Spankbang.enable)
                channels.Add(new ChannelItem("spankbang.com", conf.Spankbang.overridehost ?? $"{host}/sbg"));

            if (conf.Eporner.enable)
                channels.Add(new ChannelItem("eporner.com", conf.Eporner.overridehost ?? $"{host}/epr"));

            if (conf.Porntrex.enable)
                channels.Add(new ChannelItem("porntrex.com", conf.Porntrex.overridehost ?? $"{host}/ptx"));

            if (conf.XvideosRED.enable)
                channels.Add(new ChannelItem("xdsred", conf.XvideosRED.overridehost ?? $"{host}/xdsred"));

            if (conf.Xnxx.enable)
                channels.Add(new ChannelItem("xnxx.com", conf.Xnxx.overridehost ?? $"{host}/xnx"));

            if (conf.Tizam.enable)
                channels.Add(new ChannelItem("tizam.pw", conf.Tizam.overridehost ?? $"{host}/tizam"));

            if (conf.BongaCams.enable)
                channels.Add(new ChannelItem("bongacams.com", conf.BongaCams.overridehost ?? $"{host}/bgs"));

            if (conf.Chaturbate.enable)
                channels.Add(new ChannelItem("chaturbate.com", conf.Chaturbate.overridehost ?? $"{host}/chu"));

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
