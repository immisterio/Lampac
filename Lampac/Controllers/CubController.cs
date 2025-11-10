using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Engine;
using System.Web;

namespace Lampac.Controllers
{
    public class CubController : BaseController
    {
        [HttpGet]
        [AllowAnonymous]
        [Route("cubproxy.js")]
        [Route("cubproxy/js/{token}")]
        public ActionResult CubProxy(string token)
        {
            if (!AppInit.conf.cub.enabled(requestInfo.Country))
                return Content(string.Empty, contentType: "application/javascript; charset=utf-8");

            string file = FileCache.ReadAllText("plugins/cubproxy.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}