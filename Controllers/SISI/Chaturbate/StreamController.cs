using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.AspNetCore.Mvc;

namespace Lampac.Controllers.Chaturbate
{
    public class StreamController : BaseController
    {
        [HttpGet]
        [Route("chu/potok.m3u8")]
        async public Task<ActionResult> Index(string baba)
        {
            if (!AppInit.conf.Chaturbate.enable)
                return OnError("disable");

            string html = await HttpClient.Get($"{AppInit.conf.Chaturbate.host}/{baba}/", useproxy: AppInit.conf.Chaturbate.useproxy);
            if (html == null)
                return OnError("html");

            string hls = new Regex("(https?://[^ ]+/playlist\\.m3u8)").Match(html).Groups[1].Value;
            if (string.IsNullOrWhiteSpace(hls))
                return OnError("hls");

            return Redirect(hls.Replace("\\u002D", "-").Replace("\\", ""));
        }
    }
}
