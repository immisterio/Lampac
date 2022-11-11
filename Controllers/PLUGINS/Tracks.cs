using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Text;

namespace Lampac.Controllers.PLUGINS
{
    public class TracksController : BaseController
    {
        [Route("ffprobe")]
        async public Task<ActionResult> Ffprobe(string media)
        {
            string memKey = $"tracks:ffprobe:{media}";
            if (!memoryCache.TryGetValue(memKey, out string outPut))
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.FileName = $"ffprobe/{AppInit.conf.ffprobe}";
                process.StartInfo.Arguments = $"-v quiet -print_format json -show_format -show_streams {media}";
                process.Start();

                outPut = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                memoryCache.Set(memKey, outPut, DateTime.Now.AddHours(1));
            }

            return Content(outPut, contentType: "application/json; charset=utf-8");
        }


        [HttpGet]
        [Route("tracks.js")]
        public ActionResult Tracks()
        {
            string file = System.IO.File.ReadAllText("plugins/tracks.js");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}
