using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Engine;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace Lampac.Controllers
{
    public class RchApi : BaseController
    {
        [HttpGet]
        [AllowAnonymous]
        [Route("rch/check/connected")]
        public ActionResult СheckСonnected()
        {
            var rch = new RchClient(HttpContext, host, new Shared.Models.Base.BaseSettings() { rhub = true }, requestInfo);
            if (rch.IsNotConnected())
                return ContentTo(rch.connectionMsg);

            var info = rch.InfoConnected() ?? new RchClientInfo();
            return Json(new { info.version, info.apkVersion, info.rchtype });
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("rch/result")]
        public ActionResult WriteResult([FromForm]string id, [FromForm]string value)
        {
            if (string.IsNullOrEmpty(id))
            {
                HttpContext.Response.StatusCode = 401;
                return Content(string.Empty);
            }

            if (!RchClient.rchIds.TryGetValue(id, out var tcs))
            {
                HttpContext.Response.StatusCode = 400;
                return Content(string.Empty);
            }

            tcs.SetResult(value ?? string.Empty);
            return Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("rch/gzresult")]
        async public Task<ActionResult> WriteZipResult([FromQuery]string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                HttpContext.Response.StatusCode = 401;
                return Content(string.Empty);
            }

            if (!RchClient.rchIds.TryGetValue(id, out var tcs))
            {
                HttpContext.Response.StatusCode = 400;
                return Content(string.Empty);
            }

            try
            {
                using (var gzip = new GZipStream(Request.Body, CompressionMode.Decompress, leaveOpen: true))
                {
                    using (var reader = new StreamReader(gzip, Encoding.UTF8))
                    {
                        var text = await reader.ReadToEndAsync();
                        tcs.SetResult(text ?? string.Empty);
                    }
                }
            }
            catch { }

            return Ok();
        }
    }
}
