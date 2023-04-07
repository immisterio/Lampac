using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lampac;
using Lampac.Engine;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using IO = System.IO;

namespace SISI
{
    public class SisiApiController : BaseController
    {
        #region sisi.js
        [HttpGet]
        [Route("sisi.js")]
        public ActionResult Sisi(bool lite)
        {
            if (!memoryCache.TryGetValue($"ApiController:sisi.js:{lite}", out string file))
            {
                file = IO.File.ReadAllText("plugins/" + (lite ? "sisi.lite.js" : "sisi.js"));
                memoryCache.Set($"ApiController:sisi.js:{lite}", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", $"{host}/sisi");
            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region modification.js
        [HttpGet]
        [Route("sisi/plugins/modification.js")]
        public ActionResult SisiModification()
        {
            if (!memoryCache.TryGetValue("ApiController:sisimodification.js", out string file))
            {
                file = IO.File.ReadAllText("wwwroot/sisi/plugins/modification.js");
                memoryCache.Set("ApiController:sisimodification.js", file, DateTime.Now.AddMinutes(5));
            }

            if (!AppInit.conf.sisi.xdb)
                file = file.Replace("addId();", "");

            file = Regex.Replace(file, "\\{localhost\\}/?", $"{host}/sisi");
            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        [Route("sisi")]
        public ActionResult Index()
        {
            var conf = AppInit.conf;
            var channels = new List<dynamic>();

            if (conf.sisi.xdb)
            {
                channels.Add(new
                {
                    title = "Sexy Time",
                    playlist_url = "http://vi.sisi.am/xdb"
                });

                channels.Add(new
                {
                    title = "faphouse.com",
                    playlist_url = "http://vi.sisi.am/xdb?sites=faphouse"
                });

                //channels.Add(new
                //{
                //    title = "bang.com",
                //    playlist_url = "http://vi.sisi.am/xdb?sites=bang"
                //});
            }

            if (AppInit.modules != null)
            {
                foreach (var item in AppInit.modules)
                {
                    foreach (var mod in item.sisi)
                    {
                        if (mod.enable)
                        {
                            channels.Add(new
                            {
                                title = mod.name,
                                playlist_url = mod.url.Replace("{localhost}", host)
                            });
                        }
                    }
                }
            }

            if (conf.PornHub.enable)
            {
                channels.Add(new
                {
                    title = "pornhub.com",
                    playlist_url = $"{host}/phub"
                });
            }

            if (conf.HQporner.enable)
            {
                channels.Add(new
                {
                    title = "hqporner.com",
                    playlist_url = $"{host}/hqr"
                });
            }

            if (conf.Spankbang.enable)
            {
                channels.Add(new
                {
                    title = "spankbang.com",
                    playlist_url = $"{host}/sbg"
                });
            }

            if (conf.Eporner.enable)
            {
                channels.Add(new
                {
                    title = "eporner.com",
                    playlist_url = $"{host}/epr"
                });
            }

            if (conf.Porntrex.enable)
            {
                channels.Add(new
                {
                    title = "porntrex.com",
                    playlist_url = $"{host}/ptx"
                });
            }

            if (conf.Ebalovo.enable)
            {
                channels.Add(new
                {
                    title = "ebalovo.porn",
                    playlist_url = $"{host}/elo"
                });
            }

            if (conf.Xhamster.enable)
            {
                channels.Add(new
                {
                    title = "xhamster.com",
                    playlist_url = $"{host}/xmr"
                });
            }

            if (conf.Xvideos.enable)
            {
                channels.Add(new
                {
                    title = "xvideos.com",
                    playlist_url = $"{host}/xds"
                });
            }

            if (conf.Xnxx.enable)
            {
                channels.Add(new
                {
                    title = "xnxx.com",
                    playlist_url = $"{host}/xnx"
                });
            }

            if (conf.BongaCams.enable)
            {
                channels.Add(new
                {
                    title = "bongacams.com",
                    playlist_url = $"{host}/bgs"
                });
            }

            if (conf.Chaturbate.enable)
            {
                channels.Add(new
                {
                    title = "chaturbate.com",
                    playlist_url = $"{host}/chu"
                });
            }

            return Json(new
            {
                title = "sisi",
                channels
            });
        }
    }
}
