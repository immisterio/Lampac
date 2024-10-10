using Lampac;
using Lampac.Engine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Models;
using System;
using System.Collections.Generic;

namespace Online
{
    public class BaseOnlineController : BaseController
    {
        #region MaybeInHls
        public bool MaybeInHls(bool hls, BaseSettings init)
        {
            if (!string.IsNullOrEmpty(init.apn?.host) && Shared.Model.AppInit.IsDefaultApnOrCors(init.apn?.host))
                return false;

            if (init.apnstream && Shared.Model.AppInit.IsDefaultApnOrCors(AppInit.conf.apn?.host))
                return false;

            return hls;
        }
        #endregion

        #region OnError
        public ActionResult OnError(ProxyManager proxyManager, bool refresh_proxy = true) => OnError(string.Empty, proxyManager, refresh_proxy);

        public ActionResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true)
        {
            if (refresh_proxy)
                proxyManager?.Refresh();

            return OnError(msg);
        }

        public ActionResult OnError() => OnError(string.Empty);

        public ActionResult OnError(string msg, bool gbcache = true)
        {
            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.StartsWith("{\"rch\""))
                    return Content(msg);

                HttpContext.Response.Headers.TryAdd("emsg", msg);
            }

            if (AppInit.conf.multiaccess && gbcache)
            {
                var gbc = new ResponseCache();
                memoryCache.Set(gbc.ErrorKey(HttpContext), msg ?? string.Empty, DateTime.Now.AddMinutes(1));
            }

            return Content(string.Empty, "text/html; charset=utf-8");
        }
        #endregion

        #region OnResult
        public ActionResult OnResult(CacheResult<string> cache)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return Content(cache.Value, "text/html; charset=utf-8");
        }

        public ActionResult OnResult<T>(CacheResult<T> cache, Func<string> html, bool rjson = false)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            if (rjson && cache.Value != null)
                return Json(cache.Value);

            return Content(html.Invoke(), "text/html; charset=utf-8");
        }
        #endregion


        public bool IsRhubFallback<T>(CacheResult<T> cache, BaseSettings init)
        {
            if (cache.IsSuccess)
                return false;

            if (cache.ErrorMsg != null && cache.ErrorMsg.StartsWith("{\"rch\""))
                return false;

            if (cache.Value == null && init.rhub && init.rhub_fallback)
            {
                init.rhub = false;
                return true;
            }

            return false;
        }
    }
}
