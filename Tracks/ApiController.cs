using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.Threading.Tasks;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Lampac.Engine.CORE;
using Shared.Engine;

namespace Lampac.Controllers
{
    public class TracksController : BaseController
    {
        [HttpGet]
        [Route("tracks.js")]
        [Route("tracks/js/{token}")]
        public ActionResult Tracks(string token)
        {
            if (!AppInit.conf.ffprobe.enable)
                return Content(string.Empty);

            var sb = new StringBuilder(FileCache.ReadAllText("plugins/tracks.js"));

            sb.Replace("{localhost}", host)
              .Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
        }


        [Route("ffprobe")]
        async public Task<ActionResult> Ffprobe(string media)
        {
            if (!AppInit.conf.ffprobe.enable || string.IsNullOrWhiteSpace(media) || !media.StartsWith("http"))
                return Content(string.Empty);

            if (media.Contains("/dlna/stream"))
            {
                string path = Regex.Match(media, "\\?path=([^&]+)").Groups[1].Value;
                if (!System.IO.File.Exists("dlna/" + HttpUtility.UrlDecode(path)))
                    return Content(string.Empty);

                media = accsArgs($"{host}/dlna/stream?path={path}");
            }
            else if (media.Contains("/stream/"))
            {
                media = Regex.Replace(media, "[^a-z0-9_:\\-\\/\\.\\=\\?\\&]+", "", RegexOptions.IgnoreCase);
                media = Regex.Replace(media, "^(https?://[a-z0-9_:\\-\\.]+/stream/)[^\\?]+", "$1", RegexOptions.IgnoreCase);

                if (!string.IsNullOrWhiteSpace(AppInit.conf.ffprobe.tsuri))
                    media = Regex.Replace(media, "^https?://[^/]+", AppInit.conf.ffprobe.tsuri, RegexOptions.IgnoreCase);
            }
            else if (media.Contains("/proxy/") && media.Contains(".mkv"))
            {
                string hash = Regex.Match(media, "/([a-z0-9]+\\.mkv)").Groups[1].Value;
                media = ProxyLink.Decrypt(hash, null).uri;
                if (string.IsNullOrWhiteSpace(media))
                    return Content(string.Empty);
            }
            else
            {
                return Content(string.Empty);
            }

            string memKey = $"tracks:ffprobe:{media}";
            if (!hybridCache.TryGetValue(memKey, out string outPut))
            {
                #region getFolder
                static string getFolder(string magnethash)
                {
                    return $"cache/tracks/{magnethash}";
                }
                #endregion

                string magnethash = null;
                if (media.Contains("/stream/"))
                {
                    magnethash = Regex.Match(media, "link=([a-z0-9]+)").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(magnethash) && System.IO.File.Exists(getFolder(magnethash)))
                        outPut = BrotliTo.Decompress(getFolder(magnethash));
                }

                if (string.IsNullOrWhiteSpace(outPut))
                {
                    var process = new System.Diagnostics.Process();
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                    process.StartInfo.FileName = AppInit.Win32NT ? "data/ffprobe.exe" : "ffprobe";
                    process.StartInfo.Arguments = $"-v quiet -print_format json -show_format -show_streams \"{media}\"";
                    process.Start();

                    outPut = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    if (outPut == null)
                        outPut = string.Empty;

                    if (Regex.Replace(outPut, "[\n\r\t ]+", "") == "{}")
                        outPut = string.Empty;

                    if (!string.IsNullOrWhiteSpace(outPut) && !string.IsNullOrWhiteSpace(magnethash))
                    {
                        BrotliTo.Compress(getFolder(magnethash), outPut);
                    }
                    else
                    {
                        // заглушка
                        hybridCache.Set(memKey, outPut, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 1));
                    }
                }
            }

            return Content(outPut, contentType: "application/json; charset=utf-8");
        }
    }
}
