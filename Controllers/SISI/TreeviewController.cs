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
                channels = channels
            });
        }
    }
}
