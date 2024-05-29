using Lampac;
using Lampac.Engine;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Model.SISI;
using System;
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
            var model = new OnErrorResult() { msg = msg };

            if (AppInit.conf.multiaccess)
            {
                var gbc = new ResponseCache();
                memoryCache.Set(gbc.ErrorKey(HttpContext), model, DateTime.Now.AddMinutes(1));
            }

            return Json(model);
        }

        public JsonResult OnResult(List<PlaylistItem> playlists, Istreamproxy conf, List<MenuItem> menu, WebProxy proxy = null, string plugin = null, int total_pages = 0)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists");

            return new JsonResult(new OnListResult()
            {
                menu = menu,
                total_pages = total_pages,
                list = playlists.Select(pl => new PlaylistItem
                {
                    name = pl.name,
                    video = HostStreamProxy(conf, pl.video, proxy: proxy, plugin: plugin, sisi: true),
                    model = pl.model,
                    picture = HostImgProxy(pl.picture, plugin: plugin),
                    preview = pl.preview,
                    time = pl.time,
                    json = pl.json,
                    related = pl.related,
                    quality = pl.quality,
                    qualitys = pl.qualitys,
                    bookmark = pl.bookmark
                })
            });
        }

        public JsonResult OnResult(List<PlaylistItem> playlists, List<MenuItem> menu, List<HeadersModel> headers = null, string plugin = null, int total_pages = 0)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists");

            return new JsonResult(new OnListResult()
            {
                menu = menu,
                total_pages = total_pages,
                list = playlists.Select(pl => new PlaylistItem
                {
                    name = pl.name,
                    video = pl.video.StartsWith("http") ? pl.video : $"{AppInit.Host(HttpContext)}/{pl.video}",
                    model = pl.model,
                    picture = HostImgProxy(pl.picture, plugin: plugin, headers: headers),
                    preview = pl.preview,
                    time = pl.time,
                    json = pl.json,
                    related = pl.related,
                    quality = pl.quality,
                    qualitys = pl.qualitys,
                    bookmark = pl.bookmark
                })
            });
        }

        public JsonResult OnResult(Dictionary<string, string> stream_links, Istreamproxy proxyconf, WebProxy proxy)
        {
            return OnResult(new StreamItem() { qualitys = stream_links }, proxyconf, proxy);
        }

        public JsonResult OnResult(StreamItem stream_links, Istreamproxy proxyconf, WebProxy proxy, List<HeadersModel> headers = null, string plugin = null)
        {
            Dictionary<string, string> qualitys_proxy = null;

            if (!proxyconf.streamproxy && (proxyconf.geostreamproxy == null || proxyconf.geostreamproxy.Count == 0))
            {
                if (proxyconf.qualitys_proxy)
                {
                    var bsc = new BaseSettings() { streamproxy = true, useproxystream = proxyconf.useproxystream, apn = proxyconf.apn, apnstream = proxyconf.apnstream };
                    qualitys_proxy = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(bsc, v.Value, proxy: proxy, plugin: plugin, sisi: true));
                }
            }

            return new JsonResult(new OnStreamResult()
            {
                qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(proxyconf, v.Value, proxy: proxy, sisi: true)),
                qualitys_proxy = qualitys_proxy,
                recomends = stream_links?.recomends?.Select(pl => new PlaylistItem
                {
                    name = pl.name,
                    video = pl.video.StartsWith("http") ? pl.video : $"{AppInit.Host(HttpContext)}/{pl.video}",
                    picture = HostImgProxy(pl.picture, height: 110, plugin: plugin, headers: headers),
                    json = pl.json
                })
            });
        }
    }
}
