using System.Collections.Generic;
using Lampac.Engine;
using Microsoft.AspNetCore.Mvc;

namespace Lampac.Controllers
{
    public class TreeviewController : BaseController
    {
        [Route("sisi")]
        public ActionResult Index()
        {
            var channels = new List<dynamic>();

            if (AppInit.conf.sisi.xdb)
            {
                channels.Add(new
                {
                    title = "Sexy Time",
                    playlist_url = "http://vi.sisi.am/xdb"
                });

                channels.Add(new
                {
                    title = "xhamster.com/gold",
                    playlist_url = "http://vi.sisi.am/xdb?sites=faphouse"
                });

                channels.Add(new
                {
                    title = "pornhubpremium.com",
                    playlist_url = "http://vi.sisi.am/xdb?sites=pornhub"
                });
            }

            if (AppInit.conf.PornHub.enable)
            {
                channels.Add(new
                {
                    title = "pornhub.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/phub"
                });
            }

            if (AppInit.conf.HQporner.enable)
            {
                channels.Add(new
                {
                    title = "hqporner.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/hqr"
                });
            }

            if (AppInit.conf.Spankbang.enable)
            {
                channels.Add(new
                {
                    title = "spankbang.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/sbg"
                });
            }

            if (AppInit.conf.Eporner.enable)
            {
                channels.Add(new
                {
                    title = "eporner.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/epr"
                });
            }

            if (AppInit.conf.Porntrex.enable)
            {
                channels.Add(new
                {
                    title = "porntrex.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/ptx"
                });
            }

            if (AppInit.conf.Ebalovo.enable)
            {
                channels.Add(new
                {
                    title = "ebalovo.porn",
                    playlist_url = $"{AppInit.Host(HttpContext)}/elo"
                });
            }

            if (AppInit.conf.Xhamster.enable)
            {
                channels.Add(new
                {
                    title = "xhamster.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/xmr"
                });
            }

            if (AppInit.conf.Xvideos.enable)
            {
                channels.Add(new
                {
                    title = "xvideos.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/xds"
                });
            }

            if (AppInit.conf.Xnxx.enable)
            {
                channels.Add(new
                {
                    title = "xnxx.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/xnx"
                });
            }

            if (AppInit.conf.BongaCams.enable)
            {
                channels.Add(new
                {
                    title = "bongacams.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/bgs"
                });
            }

            if (AppInit.conf.Chaturbate.enable)
            {
                channels.Add(new
                {
                    title = "chaturbate.com",
                    playlist_url = $"{AppInit.Host(HttpContext)}/chu"
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
