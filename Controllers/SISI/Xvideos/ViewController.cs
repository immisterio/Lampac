using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;

namespace Lampac.Controllers.Xvideos
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("xds/vidosik")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Xvideos.enable)
                return OnError("disable");

            string html = await HttpClient.Get($"{AppInit.conf.Xvideos.host}/{Regex.Replace(goni, "^([^/]+)/.*", "$1/_")}", timeoutSeconds: 10, useproxy: AppInit.conf.Xvideos.useproxy);
            if (html == null)
                return OnError("html");

            string stream_link = new Regex("html5player\\.setVideoHLS\\('([^']+)'\\);").Match(html).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(stream_link))
                return OnError("stream_link");

            string m3u8 = await HttpClient.Get(stream_link, timeoutSeconds: 8, useproxy: AppInit.conf.Xvideos.useproxy);
            if (m3u8 == null)
                return OnError("m3u8");

            var playlists = new List<PlaylistItem>();
            foreach (string line in m3u8.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("hls-"))
                    continue;

                string hls = $"{Regex.Replace(stream_link.Replace("https:", "http:"), "/hls.m3u8.*", "")}/{line}";
                playlists.Add(new PlaylistItem()
                {
                    name = new Regex("hls-([0-9]+)p").Match(line).Groups[1].Value,
                    video = AppInit.conf.Xvideos.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{hls}" : hls
                });
            }

            if (playlists.Count == 0)
                return OnError("playlists");

            return Json(playlists.OrderByDescending(i => { int.TryParse(i.name, out int q); return q; }).ToDictionary(k => $"{k.name}p", v => v.video));
        }
    }
}
