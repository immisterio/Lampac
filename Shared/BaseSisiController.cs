﻿using Lampac;
using Lampac.Engine;
using Lampac.Models.Module;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Model.Online;
using Shared.Model.SISI;
using Shared.Models.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace SISI
{
    public class BaseSisiController : BaseController
    {
        public BaseSettings init { get; private set; }

        #region IsBadInitialization
        async public ValueTask<bool> IsBadInitialization(BaseSettings init, bool? rch = null)
        {
            if (AppInit.modules != null)
            {
                var args = new InitializationModel(init, rch);

                foreach (RootModule mod in AppInit.modules.Where(i => i.initialization != null))
                {
                    try
                    {
                        if (mod.assembly.GetType(mod.NamespacePath(mod.initialization)) is Type t)
                        {
                            if (t.GetMethod("Invoke") is MethodInfo m2)
                            {
                                badInitMsg = (ActionResult)m2.Invoke(null, new object[] { HttpContext, memoryCache, requestInfo, host, args });
                                if (badInitMsg != null)
                                    return true;
                            }

                            if (t.GetMethod("InvokeAsync") is MethodInfo m)
                            {
                                badInitMsg = await (Task<ActionResult>)m.Invoke(null, new object[] { HttpContext, memoryCache, requestInfo, host, args });
                                if (badInitMsg != null)
                                    return true;
                            }
                        }
                    }
                    catch { }
                }
            }

            this.init = init;

            if (!init.enable || init.rip)
            {
                badInitMsg = OnError("disable");
                return true;
            }

            if (NoAccessGroup(init, out string error_msg))
            {
                badInitMsg = OnError(error_msg, false);
                return true;
            }

            var overridehost = await IsOverridehost(init);
            if (overridehost != null)
            {
                badInitMsg = overridehost;
                return true;
            }

            return IsCacheError(init);
        }
        #endregion

        #region OnError
        public JsonResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true, bool rcache = true)
        {
            if (refresh_proxy && !init.rhub)
                proxyManager?.Refresh();

            return OnError(msg, rcache: rcache);
        }

        public JsonResult OnError(string msg, bool rcache = true)
        {
            var model = new OnErrorResult() { msg = msg };

            if (AppInit.conf.multiaccess && rcache && !init.rhub)
            {
                var gbc = new ResponseCache();
                memoryCache.Set(gbc.ErrorKey(HttpContext), model, DateTime.Now.AddSeconds(20));
            }

            return Json(model);
        }
        #endregion

        #region OnResult
        public JsonResult OnResult(IList<PlaylistItem> playlists, BaseSettings conf, IList<MenuItem> menu, WebProxy proxy = null, int total_pages = 0)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists", false);

            var resultArray = new PlaylistItem[playlists.Count];

            for (int i = 0; i < playlists.Count; i++)
            {
                var pl = playlists[i];
                resultArray[i] = new PlaylistItem
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
                };
            }

            return new JsonResult(new OnListResult()
            {
                menu = menu,
                total_pages = total_pages,
                list = resultArray
            });
        }

        public JsonResult OnResult(IList<PlaylistItem> playlists, IList<MenuItem> menu, List<HeadersModel> headers = null, int total_pages = 0, string plugin = null)
        {
            if (playlists == null || playlists.Count == 0)
                return OnError("playlists", false);

            var resultArray = new PlaylistItem[playlists.Count];

            for (int i = 0; i < playlists.Count; i++)
            {
                var pl = playlists[i];
                resultArray[i] = new PlaylistItem
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
                };
            }

            return new JsonResult(new OnListResult()
            {
                menu = menu,
                total_pages = total_pages,
                list = resultArray
            });
        }

        public JsonResult OnResult(Dictionary<string, string> stream_links, BaseSettings init, WebProxy proxy, List<HeadersModel> headers_stream = null)
        {
            return OnResult(new StreamItem() { qualitys = stream_links }, init, proxy, headers_stream: headers_stream);
        }

        public JsonResult OnResult(StreamItem stream_links, BaseSettings init, WebProxy proxy, List<HeadersModel> headers_img = null, List<HeadersModel> headers_stream = null)
        {
            Dictionary<string, string> qualitys_proxy = null;

            if (!init.streamproxy && (init.geostreamproxy == null || init.geostreamproxy.Length == 0))
            {
                if (init.qualitys_proxy)
                {
                    var bsc = new BaseSettings() { streamproxy = true, useproxystream = init.useproxystream, apn = init.apn, apnstream = init.apnstream };
                    qualitys_proxy = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(bsc, v.Value, proxy: proxy, headers: headers_stream));
                }
            }

            var recomendsArray = new PlaylistItem[stream_links?.recomends?.Count ?? 0];
            if (recomendsArray.Length > 0)
            {
                for (int i = 0; i < stream_links.recomends.Count; i++)
                {
                    var pl = stream_links.recomends[i];
                    recomendsArray[i] = new PlaylistItem
                    {
                        name = pl.name,
                        video = pl.video.StartsWith("http") ? pl.video : $"{AppInit.Host(HttpContext)}/{pl.video}",
                        picture = HostImgProxy(pl.picture, height: 110, plugin: init?.plugin, headers: headers_img),
                        json = pl.json
                    };
                }
            }

            return new JsonResult(new OnStreamResult()
            {
                qualitys = stream_links.qualitys.ToDictionary(k => k.Key, v => HostStreamProxy(init, v.Value, proxy: proxy, headers: headers_stream)),
                qualitys_proxy = qualitys_proxy,
                recomends = recomendsArray,
                headers_stream = init.streamproxy ? null : (headers_stream?.ToDictionary() ?? init.headers_stream)
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
