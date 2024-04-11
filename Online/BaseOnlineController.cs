using Lampac;
using Lampac.Engine;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Models;
using System;
using System.Collections.Generic;

namespace Online
{
    public class BaseOnlineController : BaseController
    {
        public bool MaybeInHls(bool hls, BaseSettings init)
        {
            if (!string.IsNullOrEmpty(init.apn?.host) && Shared.Model.AppInit.IsDefaultApnOrCors(init.apn?.host))
                return false;

            if (init.apnstream && Shared.Model.AppInit.IsDefaultApnOrCors(AppInit.conf.apn?.host))
                return false;

            return hls;
        }

        #region OnError
        public ActionResult OnError() => OnError(string.Empty);

        public ActionResult OnError(string msg)
        {
            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.StartsWith("{\"rch\""))
                    return Content(msg);

                HttpContext.Response.Headers.TryAdd("emsg", msg);
            }

            return Content(string.Empty, "text/html; charset=utf-8");
        }

        public ActionResult OnError(ProxyManager proxyManager, bool refresh_proxy = true) => OnError(string.Empty, proxyManager, refresh_proxy);

        public ActionResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true)
        {
            if (refresh_proxy)
                proxyManager?.Refresh();

            return OnError(msg);
        }
        #endregion


        public ActionResult OnResult(CacheResult<string> cache)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return Content(cache.Value, "text/html; charset=utf-8");
        }

        public ActionResult OnResult<T>(CacheResult<T> cache, Func<string> html)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg);

            return Content(html.Invoke(), "text/html; charset=utf-8");
        }
    }
}
