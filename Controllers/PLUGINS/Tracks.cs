using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Lampac.Controllers.PLUGINS
{
    public class TracksController : BaseController
    {
        [Route("ffprobe")]
        async public Task<ActionResult> Ffprobe(string media)
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.ffprobe) || string.IsNullOrWhiteSpace(media) || !media.StartsWith("http"))
                return Content(string.Empty);

            if (media.Contains("/dlna/stream"))
            {
                if (!System.IO.File.Exists("dlna/" + Regex.Replace(media, "^https?://[a-z0-9_:\\-\\.]+/dlna/stream\\?path=", "", RegexOptions.IgnoreCase)))
                    return Content(string.Empty);
            }
            else if (media.Contains("/stream/"))
            {
                media = Regex.Replace(media, "[^a-z0-9_:\\-\\/\\.\\=\\?\\&]+", "", RegexOptions.IgnoreCase);
                media = Regex.Replace(media, "^(https?://[a-z0-9_:\\-\\.]+/stream/)[^\\?]+", "$1", RegexOptions.IgnoreCase);
            }
            else
            {
                return Content(string.Empty);
            }

            string memKey = $"tracks:ffprobe:{media}";
            if (!memoryCache.TryGetValue(memKey, out string outPut))
            {
                var process = new System.Diagnostics.Process();
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                process.StartInfo.FileName = AppInit.conf.ffprobe == "linux" ? "ffprobe" : $"ffprobe/{AppInit.conf.ffprobe}";
                process.StartInfo.Arguments = $"-v quiet -print_format json -show_format -show_streams '{media}'";
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
            file = file.Replace("{localhost}", host);

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}
