using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.Porntrex
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("ptx/vidosik")]
        async public Task<ActionResult> vidosik(string goni)
        {
            if (!AppInit.conf.Porntrex.enable)
                return OnError("disable");

            string memKey = $"Porntrex:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out Dictionary<string, string> stream_links))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Porntrex.host}/{goni}", timeoutSeconds: 10, useproxy: AppInit.conf.Porntrex.useproxy);
                if (html == null)
                    return OnError("html");

                stream_links = new Dictionary<string, string>();
                var match = new Regex("(https?://[^/]+/get_file/[^\\.]+_([0-9]+p)\\.mp4)").Match(html);
                while (match.Success)
                {
                    stream_links.TryAdd(match.Groups[2].Value, $"{AppInit.Host(HttpContext)}/ptx/strem?link={HttpUtility.UrlEncode(match.Groups[1].Value)}");
                    match = match.NextMatch();
                    //break;
                }

                if (stream_links.Count == 0)
                {
                    string link = Regex.Match(html, "(https?://[^/]+/get_file/[^\\.]+\\.mp4)").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(link))
                        stream_links.TryAdd("auto", $"{AppInit.Host(HttpContext)}/ptx/strem?link={HttpUtility.UrlEncode(link)}");
                }

                if (stream_links.Count == 0)
                    return OnError("stream_links");

                stream_links = stream_links.Reverse().ToDictionary(k => k.Key, v => v.Value);

                memoryCache.Set(memKey, stream_links, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 5));
            }
            
            return Json(stream_links);
        }


        [HttpGet]
        [Route("ptx/strem")]
        async public Task<ActionResult> strem(string link)
        {
            string memKey = $"Porntrex:strem:{link}";
            if (!memoryCache.TryGetValue(memKey, out string location))
            {
                location = await HttpClient.GetLocation(link, timeoutSeconds: 10, httpversion: 2, addHeaders: new List<(string name, string val)>() 
                {
                    ("sec-fetch-dest", "document"),
                    ("sec-fetch-mode", "navigate"),
                    ("sec-fetch-site", "none"),
                    ("sec-fetch-user", "?1"),
                    ("upgrade-insecure-requests", "1"),
                    ("user-agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/108.0.0.0 Safari/537.36")
                });

                if (location == null || link == location)
                    return OnError("location");

                memoryCache.Set(memKey, location, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 5));
            }

            return Redirect(AppInit.conf.Porntrex.streamproxy ? $"{AppInit.Host(HttpContext)}/proxy/{location}" : location);
        }
    }
}
