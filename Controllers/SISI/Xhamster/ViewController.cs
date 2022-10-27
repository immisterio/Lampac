using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Lampac.Engine;
using Lampac.Engine.CORE;

namespace Lampac.Controllers.Xhamster
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("xmr/vidosik.m3u8")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Xhamster.enable)
                return OnError("disable");

            string html = await HttpClient.Get($"{AppInit.conf.Xhamster.host}/{goni}", useproxy: AppInit.conf.Xhamster.useproxy);
            if (html == null)
                return OnError("html");

            #region Достаем ссылки
            string stream_link = new Regex("\"hls\":{\"url\":\"([^\"]+)\"").Match(html).Groups[1].Value.Replace("\\", "");
            if (string.IsNullOrWhiteSpace(stream_link))
                return OnError("stream_link");

            if (stream_link.StartsWith("/"))
                stream_link = AppInit.conf.Xhamster.host + stream_link;

            string m3u8 = await HttpClient.Get(stream_link, timeoutSeconds: 10, useproxy: AppInit.conf.Xhamster.useproxy);
            if (m3u8 == null)
                return OnError("m3u8");

            (string link, int RESOLUTION) stream = (null, 0);

            foreach (string line in m3u8.Split("#EXT-X"))
            {
                if (int.TryParse(Regex.Match(line, "RESOLUTION=[0-9]+x([0-9]+)").Groups[1].Value, out int RESOLUTION) && RESOLUTION > 0)
                {
                    string link = Regex.Match(line, "\"[\n\r\t ]+([^\n\r]+)").Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(link))
                        continue;

                    if (!link.StartsWith("http"))
                    {
                        if (link.StartsWith("/"))
                        {
                            link = Regex.Replace(stream_link, "^(https?://[^/]+)/.*$", "$1/") + link;
                        }
                        else
                        {
                            link = Regex.Replace(stream_link, "/[^/]+$", "/") + link;
                        }
                    }

                    if (RESOLUTION > stream.RESOLUTION && RESOLUTION != 2160)
                    {
                        link = link.Replace("https:", "http:");
                        stream = (AppInit.conf.Xhamster.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{link}" : link, RESOLUTION);
                    }
                }
            }
            #endregion

            if (string.IsNullOrWhiteSpace(stream.link))
                return OnError("stream");

            string hls = null;

            if (!stream.link.Contains(".m3u"))
            {
                m3u8 = await HttpClient.Get(stream.link, useproxy: AppInit.conf.Xhamster.useproxy, timeoutSeconds: 8);
                if (m3u8 != null)
                {
                    // Ссылка на сервер источника
                    string masterHost = new Regex("^(https?://[^/]+)").Match(stream.link).Groups[1].Value;

                    // Меняем ссылки которые начинаются с "/" на "http"
                    hls = Regex.Replace(m3u8, "([\n\r\t ]+)/", $"$1{masterHost}/");
                }
            }

            if (hls != null)
                return Content(hls, "application/vnd.apple.mpegurl");

            return Redirect(stream.link);
        }
    }
}
