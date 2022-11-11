using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;

namespace Lampac.Controllers.PLUGINS
{
    public class TmdbproxyController : BaseController
    {
        [HttpGet]
        [Route("tmdbproxy.js")]
        public ActionResult Tracks()
        {
            string file = System.IO.File.ReadAllText("plugins/tmdbproxy.js");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}
