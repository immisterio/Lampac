using Lampac.Engine;
using Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net.NetworkInformation;
using Shared.Models.AppConf;
using Shared.PlaywrightCore;
using Shared.Engine;

namespace Lampac.Controllers
{
    public class OpenStatController : BaseController
    {
        public OpenStatConf openstat => AppInit.conf.openstat;

        public bool IsDeny(out string ermsg) 
        {
            ermsg = "Включите openstat в init.conf\n\n\"openstat\": {\n   \"enable\": true\n}";

            if (!openstat.enable || (!string.IsNullOrEmpty(openstat.token) && openstat.token != HttpContext.Request.Query["token"].ToString()))
                return true;

            return false;
        }

        #region browser/context
        [Route("/stats/browser/context")]
        public ActionResult BrowserContext()
        {
            if (IsDeny(out string ermsg))
                return Content(ermsg, "text/plain; charset=utf-8");

            return Json(new
            {
                Chromium = new
                {
                    open = Chromium.browser?.Contexts?.Count,
                    req_keepopen = Chromium.stats_keepopen,
                    req_newcontext = Chromium.stats_newcontext,
                    ping = new 
                    {
                        Chromium.stats_ping.status,
                        Chromium.stats_ping.time,
                        Chromium.stats_ping.ex
                    }
                },
                Firefox = new
                {
                    open = Firefox.browser?.Contexts?.Count,
                    req_keepopen = Firefox.stats_keepopen,
                    req_newcontext = Firefox.stats_newcontext
                }
            });
        }
        #endregion

        #region request
        [Route("/stats/request")]
        public ActionResult Requests()
        {
            if (IsDeny(out string ermsg))
                return Content(ermsg, "text/plain; charset=utf-8");

            long req_min = memoryCache.Get<long>($"stats:request:{DateTime.Now.AddMinutes(-1).Minute}");

            long req_hour = req_min;
            for (int i = 1; i < 58; i++)
            {
                if (memoryCache.TryGetValue($"stats:request:{DateTime.Now.AddMinutes(-i).Minute}", out long _r))
                    req_hour += _r;
            }

            return Json(new 
            { 
                req_min, 
                req_hour, 
                soks_online = soks._connections?.Count,
                tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections().Length
            });
        }
        #endregion

        #region rch
        [Route("/stats/rch")]
        public ActionResult Rhc()
        {
            if (IsDeny(out string ermsg))
                return Content(ermsg, "text/plain; charset=utf-8");

            return Json(new { clients = RchClient.clients.Count, rchIds = RchClient.rchIds.Count });
        }
        #endregion
    }
}