using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine;
using Shared.Engine;

namespace Lampac.Controllers
{
    public class RchApi : BaseController
    {
        [Route("rch.js")]
        public ActionResult RchJs() => Content(AppInit.conf.rch.enable ? FileCache.ReadAllText("plugins/rch.js") : string.Empty, contentType: "application/javascript; charset=utf-8");


        [HttpPost]
        [Route("rch/result")]
        public ActionResult WriteResult([FromForm]string id, [FromForm]string value)
        {
            if (string.IsNullOrEmpty(id))
                return Content(string.Empty);

            if (!RchClient.rchIds.TryGetValue(id, out var tcs))
                return Content(string.Empty);

            tcs.SetResult(value);
            return Content(string.Empty);
        }
    }
}
