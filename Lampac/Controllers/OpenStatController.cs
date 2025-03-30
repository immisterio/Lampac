using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Models.AppConf;
using Shared.Engine;
using Microsoft.AspNetCore.Http;
using System;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers
{
    public class OpenStatController : BaseController
    {
        public OpenStatConf openstat => AppInit.conf.openstat;

        public bool IsDeny() 
        {
            if (!openstat.enable || (!string.IsNullOrEmpty(openstat.token) && openstat.token != HttpContext.Request.Query["token"].ToString()))
                return true;

            return false;
        }

        #region browser/context
        [Route("/stats/browser/context")]
        public ActionResult BrowserContext()
        {
            if (IsDeny())
                return ContentTo("{}");

            return Json(new
            {
                Chromium = new
                {
                    open = Chromium.pages_keepopen.Count,
                    req_keepopen = Chromium.stats_keepopen,
                    req_newcontext = Chromium.stats_newcontext,
                },
                Firefox = new
                {
                    open = Firefox.pages_keepopen.Count,
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
            if (IsDeny())
                return ContentTo("{}");

            long req_min = memoryCache.Get<long>($"stats:request:{DateTime.Now.AddMinutes(-1).Minute}");

            long req_hour = req_min;
            for (int i = 1; i < 58; i++)
            {
                if (memoryCache.TryGetValue($"stats:request:{DateTime.Now.AddMinutes(-i).Minute}", out long _r))
                    req_hour += _r;
            }

            return Json(new { req_min, req_hour });
        }
        #endregion
    }
}