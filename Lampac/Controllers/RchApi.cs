using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine;

namespace Lampac.Controllers
{
    public class RchApi : BaseController
    {
        [HttpGet]
        [Route("rch/check/connected")]
        public ActionResult СheckСonnected()
        {
            var rch = new RchClient(HttpContext, host, new Shared.Model.Base.BaseSettings() { rhub = true }, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var info = rch.InfoConnected();
            return Json(new { info.version, info.apkVersion, info.rchtype });
        }

        [HttpPost]
        [Route("rch/result")]
        public ActionResult WriteResult([FromForm]string id, [FromForm]string value)
        {
            if (string.IsNullOrEmpty(id))
                return Content(string.Empty);

            if (!RchClient.rchIds.TryGetValue(id, out var tcs))
                return Content(string.Empty);

            tcs.SetResult(value ?? string.Empty);
            return Ok();
        }
    }
}
