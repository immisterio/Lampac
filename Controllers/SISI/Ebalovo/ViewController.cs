using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.Ebalovo
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("elo/vidosik")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Ebalovo.enable)
                return OnError("disable");

            string memKey = $"Ebalovo:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Ebalovo.host}/{goni}");
                if (html == null)
                    return OnError("html");

                string stream_link = null;
                var match = new Regex($"(https?://[^/]+/get_file/[^\\.]+_([0-9]+p)\\.mp4)").Match(html);
                while (match.Success)
                {
                    stream_link = match.Groups[1].Value;
                    match = match.NextMatch();
                }

                if (string.IsNullOrWhiteSpace(stream_link))
                    return OnError("stream_link");

                location = await HttpClient.GetLocation(stream_link, referer: $"{AppInit.conf.Ebalovo.host}/");
                if (location == null || stream_link == location || location.Contains("/get_file/"))
                    return OnError("location");

                memoryCache.Set(memKey, location, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 5));
            }

            return Redirect(AppInit.conf.Ebalovo.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{location}" : location);
        }
    }
}
