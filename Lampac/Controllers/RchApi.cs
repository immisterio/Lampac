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
    public class RchBaseApi : BaseController
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
    }

    public class RchApi : Controller
    {
        [HttpPost]
        [AllowAnonymous]
        [Route("rch/result")]
        async public Task<ActionResult> WriteResult([FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(401);

            if (!RchClient.rchIds.TryGetValue(id, out var rchHub))
                return BadRequest(400);

            try
            {
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    var (success, len) = await TextReaderSpan.ReadAllCharsAsync(rchHub.owner, reader, cancellationToken: HttpContext.RequestAborted);

                    if (!success)
                    {
                        rchHub.tcs.TrySetResult((0, null));
                        return BadRequest(400);
                    }

                    rchHub.tcs.TrySetResult((len, null));
                }
            }
            catch
            {
                rchHub.tcs.TrySetResult((0, null));
                return BadRequest(400);
            }

            return Ok();
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("rch/gzresult")]
        async public Task<ActionResult> WriteZipResult([FromQuery] string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest(401);

            if (!RchClient.rchIds.TryGetValue(id, out var rchHub))
                return BadRequest(400);

            try
            {
                using (var gzip = new GZipStream(Request.Body, CompressionMode.Decompress, leaveOpen: true))
                {
                    using (var reader = new StreamReader(gzip, Encoding.UTF8))
                    {
                        var (success, len) = await TextReaderSpan.ReadAllCharsAsync(rchHub.owner, reader, cancellationToken: HttpContext.RequestAborted);

                        if (!success)
                        {
                            rchHub.tcs.TrySetResult((0, null));
                            return BadRequest(400);
                        }

                        rchHub.tcs.TrySetResult((len, null));
                    }
                }
            }
            catch 
            {
                rchHub.tcs.TrySetResult((0, null));
                return BadRequest(400);
            }

            return Ok();
        }
    }
}
