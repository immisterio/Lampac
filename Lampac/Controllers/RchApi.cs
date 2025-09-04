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
        [Route("rch/check/connected")]
        public ActionResult СheckСonnected()
        {
            var rch = new RchClient(HttpContext, host, new Shared.Models.Base.BaseSettings() { rhub = true }, requestInfo);
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

            using (var compressedStream = new MemoryStream())
            {
                await Request.Body.CopyToAsync(compressedStream);
                compressedStream.Position = 0;

                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
                {
                    using (var resultStream = new MemoryStream())
                    {
                        gzipStream.CopyTo(resultStream);
                        string decompressedData = Encoding.UTF8.GetString(resultStream.ToArray());
                        tcs.SetResult(decompressedData);
                    }
                }
            }

            return Ok();
        }
    }
}
