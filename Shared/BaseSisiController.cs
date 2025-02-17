﻿using Lampac;
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
        public SisiSettings init { get; private set; }

        #region IsBadInitialization
        public bool IsBadInitialization(SisiSettings init, out ActionResult result)
        {
            this.init = init.Clone();

            if (!init.enable)
            {
                result = OnError("disable");
                return true;
            }

            if (NoAccessGroup(init, out string error_msg))
            {
                result = OnError(error_msg, false);
                return true;
            }

            if (IsOverridehost(init, out string overridehost))
            {
                result = Redirect(overridehost);
                return true;
            }

            return IsCacheError(init, out result);
        }
        #endregion

        #region OnError
        public JsonResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true)
        {
            if (refresh_proxy && !init.rhub)
                proxyManager?.Refresh();

            return OnError(msg);
        }

        public JsonResult OnError(string msg, bool rcache = true)
        {
            var model = new OnErrorResult() { msg = msg };

            if (AppInit.conf.multiaccess && rcache && !init.rhub)
            {
                var gbc = new ResponseCache();
                memoryCache.Set(gbc.ErrorKey(HttpContext), model, DateTime.Now.AddMinutes(1));
            }

            return Json(model);
        }
        #endregion

        #region OnResult
        public JsonResult OnResult(List<PlaylistItem> playlists, BaseSettings conf, List<MenuItem> menu, WebProxy proxy = null, int total_pages = 0)
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
                    video = HostStreamProxy(conf, pl.video, proxy: proxy),
                    model = pl.model,
                    picture = HostImgProxy(pl.picture, plugin: conf?.plugin),
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

        public JsonResult OnResult(List<PlaylistItem> playlists, List<MenuItem> menu, List<HeadersModel> headers = null, int total_pages = 0, string plugin = null)
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

        public JsonResult OnResult(Dictionary<string, string> stream_links, BaseSettings proxyconf, WebProxy proxy)
        {
            return OnResult(new StreamItem() { qualitys = stream_links }, proxyconf, proxy);
        }

        public JsonResult OnResult(StreamItem stream_links, BaseSettings proxyconf, WebProxy proxy, List<HeadersModel> headers = null)
        {
            Dictionary<string, string> qualitys_proxy = null;

            if (!proxyconf.streamproxy && (proxyconf.geostreamproxy == null || proxyconf.geostreamproxy.Length == 0))
            {
                if (proxyconf.qualitys_proxy)
                {
                    var bsc = new BaseSettings() { streamproxy = true, useproxystream = proxyconf.useproxystream, apn = proxyconf.apn, apnstream = proxyconf.apnstream };
                    qualitys_proxy = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(bsc, v.Value, proxy: proxy));
                }
            }

            return new JsonResult(new OnStreamResult()
            {
                qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(proxyconf, v.Value, proxy: proxy)),
                qualitys_proxy = qualitys_proxy,
                recomends = stream_links?.recomends?.Select(pl => new PlaylistItem
                {
                    name = pl.name,
                    video = pl.video.StartsWith("http") ? pl.video : $"{AppInit.Host(HttpContext)}/{pl.video}",
                    picture = HostImgProxy(pl.picture, height: 110, plugin: proxyconf?.plugin, headers: headers),
                    json = pl.json
                })
            });
        }
        #endregion

        #region IsRhubFallback
        public bool IsRhubFallback(BaseSettings init)
        {
            if (init.rhub && init.rhub_fallback)
            {
                init.rhub = false;
                return true;
            }

            return false;
        }
        #endregion
    }
}
