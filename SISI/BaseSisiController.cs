using Lampac;
using Lampac.Engine;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
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

        public JsonResult OnResult(List<PlaylistItem> playlists, Istreamproxy conf, List<MenuItem> menu, WebProxy proxy = null, string plugin = null)
        {
            return new JsonResult(new
            {
                menu,
                list = playlists.Select(pl => new
                {
                    pl.name,
                    video = HostStreamProxy(conf, pl.video, proxy: proxy, plugin: plugin),
                    picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture),
                    pl.preview,
                    pl.time,
                    pl.json,
                    pl.quality,
                    pl.qualitys,
                    pl.bookmark
                })
            });
        }

        public JsonResult OnResult(List<PlaylistItem> playlists, List<MenuItem> menu, List<HeadersModel> headers = null)
        {
            return new JsonResult(new
            {
                menu,
                list = playlists.Select(pl => new
                {
                    pl.name,
                    video = pl.video.StartsWith("http") ? pl.video : $"{AppInit.Host(HttpContext)}/{pl.video}",
                    picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.picture, headers: headers),
                    pl.preview,
                    pl.time,
                    pl.json,
                    pl.quality,
                    pl.qualitys,
                    pl.bookmark
                })
            });
        }

        public JsonResult OnResult(StreamItem stream_links, Istreamproxy proxyconf, WebProxy proxy, List<HeadersModel> headers = null, string plugin = null)
        {
            Dictionary<string, string> qualitys_proxy = null;

            if (!proxyconf.streamproxy && proxyconf.qualitys_proxy)
                qualitys_proxy = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(new BaseSettings() { streamproxy = true }, v.Value, proxy: proxy, plugin: plugin));

            return new JsonResult(new
            {
                qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(proxyconf, v.Value, proxy: proxy)),
                qualitys_proxy,
                recomends = stream_links?.recomends?.Select(pl => new
                {
                    pl.name,
                    video = pl.video.StartsWith("http") ? pl.video : $"{AppInit.Host(HttpContext)}/{pl.video}",
                    picture = HostImgProxy(0, 110, pl.picture, headers: headers),
                    pl.json
                })
            });
        }

        public JsonResult OnResult(Dictionary<string, string> stream_links, Istreamproxy proxyconf, WebProxy proxy)
        {
            return OnResult(new StreamItem() { qualitys = stream_links }, proxyconf, proxy);
        }
    }
}
