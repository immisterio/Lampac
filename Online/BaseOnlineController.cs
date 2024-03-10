using Lampac;
using Lampac.Engine;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Model.Base;
using System.Collections.Generic;

namespace Online
{
    public class BaseOnlineController : BaseController
    {
        public bool MaybeInHls(bool hls, BaseSettings init)
        {
            if (!string.IsNullOrEmpty(init.apn) && Shared.Model.AppInit.IsDefaultApnOrCors(init.apn))
                return false;

            if (init.apnstream && Shared.Model.AppInit.IsDefaultApnOrCors(AppInit.conf.apn))
                return false;

            return hls;
        }

        public ActionResult OnError() => OnError(string.Empty);

        public ActionResult OnError(string msg)
        {
            if (msg != string.Empty)
                HttpContext.Response.Headers.TryAdd("emsg", msg);

            return Content(string.Empty, "text/html; charset=utf-8");
        }

        public ActionResult OnError(ProxyManager proxyManager, bool refresh_proxy = true) => OnError(string.Empty, proxyManager, refresh_proxy);

        public ActionResult OnError(string msg, ProxyManager proxyManager, bool refresh_proxy = true)
        {
            if (refresh_proxy)
                proxyManager?.Refresh();

            return OnError(msg);
        }
    }
}
