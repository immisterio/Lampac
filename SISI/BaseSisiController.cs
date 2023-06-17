using Lampac;
using Lampac.Engine;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Model.Base;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace SISI
{
    public class BaseSisiController : BaseController
    {
        public JsonResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true)
        {
            if (refresh_proxy)
                proxyManager?.Refresh();

            return OnError(msg);
        }

        public JsonResult OnError(string msg)
        {
            return new JsonResult(new { success = false, msg });
        }

        public JsonResult OnResult(List<PlaylistItem> playlists, Istreamproxy conf, List<MenuItem> menu, WebProxy proxy = null)
        {
            return new JsonResult(new
            {
                menu,
                list = playlists.Select(pl => new
                {
                    pl.name,
                    video = HostStreamProxy(conf, pl.video, proxy: proxy),
                    picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture),
                    pl.time,
                    pl.json,
                    pl.quality,
                    pl.qualitys
                })
            });
        }

        public JsonResult OnResult(List<PlaylistItem> playlists, List<MenuItem> menu, List<(string name, string val)> headers = null)
        {
            return new JsonResult(new
            {
                menu,
                list = playlists.Select(pl => new
                {
                    pl.name,
                    pl.video,
                    picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture, headers: headers),
                    pl.time,
                    pl.json,
                    pl.quality,
                    pl.qualitys
                })
            });
        }

        public JsonResult OnResult(StreamItem stream_links, Istreamproxy proxyconf, WebProxy proxy, List<(string name, string val)> headers = null)
        {
            return new JsonResult(new
            {
                qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(proxyconf, v.Value, proxy: proxy)),
                recomends = stream_links.recomends.Select(pl => new
                {
                    pl.name,
                    pl.video,
                    picture = HostImgProxy(0, 110, pl.picture, headers: headers),
                    pl.json
                })
            });
        }
    }
}
