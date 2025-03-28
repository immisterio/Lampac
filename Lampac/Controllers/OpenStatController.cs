using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Models.AppConf;
using Shared.Engine;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

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
    }
}