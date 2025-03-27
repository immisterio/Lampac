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

            return ContentTo(JsonConvert.SerializeObject(new
            {
                Chromium = new
                {
                    activ = Chromium.pages_keepopen.Count,
                    keepopen = Chromium.stats_keepopen,
                    newcontext = Chromium.stats_newcontext,
                },
                Firefox = new
                {
                    activ = Firefox.pages_keepopen.Count,
                    keepopen = Firefox.stats_keepopen, 
                    newcontext = Firefox.stats_newcontext 
                }

            }, Formatting.Indented));
        }
    }
}