﻿using Lampac;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.Module;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Engine.CORE;
using Shared.Model.Base;
using Shared.Models;
using Shared.Models.Module;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;

namespace Online
{
    public class BaseOnlineController : BaseController
    {
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

            if (!init.enable || init.rip)
            {
                badInitMsg = OnError("disable");
                return true;
            }

            if (NoAccessGroup(init, out string error_msg))
            {
                badInitMsg = new JsonResult(new { accsdb = true, msg = error_msg });
                return true;
            }

            var overridehost = await IsOverridehost(init);
            if (overridehost != null)
            {
                badInitMsg = overridehost;
                return true;
            }

            if (rch != null)
            {
                if ((bool)rch)
                {
                    if (init.rhub && !AppInit.conf.rch.enable)
                    {
                        badInitMsg = ShowError(RchClient.ErrorMsg);
                        return true;
                    }
                }
                else
                {
                    if (init.rhub)
                    {
                        badInitMsg = ShowError(RchClient.ErrorMsg);
                        return true;
                    }
                }
            }

            return IsCacheError(init);
        }
        #endregion


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

        #region OnLog
        public void OnLog(string msg)
        {
            HttpClient.onlog?.Invoke(null, msg + "\n");
        }
        #endregion

        #region OnError
        public ActionResult OnError(ProxyManager proxyManager, bool refresh_proxy = true, string weblog = null) => OnError(string.Empty, proxyManager, refresh_proxy, weblog: weblog);

        public ActionResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true, string weblog = null)
        {
            if (string.IsNullOrEmpty(msg) || !msg.StartsWith("{\"rch\""))
            {
                if (refresh_proxy)
                    proxyManager?.Refresh();
            }

            return OnError(msg, weblog: weblog);
        }

        public ActionResult OnError() => OnError(string.Empty);

        public ActionResult OnError(string msg, bool gbcache = true, string weblog = null)
        {
            if (!string.IsNullOrEmpty(msg))
            {
                if (msg.StartsWith("{\"rch\""))
                    return Content(msg);

                string log = $"{HttpContext.Request.Path.Value}\n{msg}";
                if (!string.IsNullOrEmpty(weblog))
                    log += $"\n\n\n===================\n\n{weblog}";

                HttpClient.onlog?.Invoke(null, log);
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
        public ActionResult OnResult(CacheResult<string> cache, bool gbcache = true)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg, gbcache: gbcache);

            return Content(cache.Value, "text/html; charset=utf-8");
        }

        public ActionResult OnResult<T>(CacheResult<T> cache, Func<string> html, bool origsource = false, bool gbcache = true)
        {
            if (!cache.IsSuccess)
                return OnError(cache.ErrorMsg, gbcache: gbcache);

            if (origsource && cache.Value != null)
                return Json(cache.Value);

            return ContentTo(html.Invoke());
        }
        #endregion

        public ActionResult ShowError(string msg) => Json(new { accsdb = true, msg });

        #region IsRhubFallback
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
        #endregion
    }
}
