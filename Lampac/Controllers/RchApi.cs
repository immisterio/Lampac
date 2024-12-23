using Microsoft.AspNetCore.Mvc;
using Lampac.Engine.CORE;
using Lampac.Engine;

namespace Lampac.Controllers
{
    public class RchApi : BaseController
    {
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
