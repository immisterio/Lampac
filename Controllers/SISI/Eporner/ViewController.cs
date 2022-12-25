using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.Eporner
{
    public class ViewController : BaseController
    {
        [HttpGet]
        [Route("epr/vidosik")]
        async public Task<ActionResult> Index(string goni)
        {
            if (!AppInit.conf.Eporner.enable)
                return OnError("disable");

            string memKey = $"Eporner:vidosik:{goni}";
            if (!memoryCache.TryGetValue(memKey, out string json))
            {
                string html = await HttpClient.Get($"{AppInit.conf.Eporner.host}/{goni}", timeoutSeconds: 10, useproxy: AppInit.conf.Eporner.useproxy);
                if (html == null)
                    return OnError("html");

                string vid = new Regex("vid = '([^']+)'").Match(html).Groups[1].Value;
                string hash = new Regex("hash = '([^']+)'").Match(html).Groups[1].Value;
                if (string.IsNullOrWhiteSpace(vid) || string.IsNullOrWhiteSpace(hash))
                    return OnError("hash");

                json = await HttpClient.Get($"{AppInit.conf.Eporner.host}/xhr/video/{vid}?hash={convertHash(hash)}&domain={Regex.Replace(AppInit.conf.Eporner.host, "^https?://", "")}&fallback=false&embed=false&supportedFormats=dash,mp4&_={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
                if (json == null)
                    return OnError("json");

                memoryCache.Set(memKey, json, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 5));
            }

            var stream_links = new Dictionary<string, string>();
            var match = new Regex("\"src\": +\"(https?://[^/]+/[^\"]+-([0-9]+p).mp4)\",").Match(json);
            while (match.Success)
            {
                stream_links.TryAdd(match.Groups[2].Value, AppInit.HostStreamProxy(HttpContext, AppInit.conf.Eporner.streamproxy, match.Groups[1].Value));
                match = match.NextMatch();
            }

            if (stream_links.Count == 0)
                return OnError("stream_links");

            return Json(stream_links);
        }


        #region convertHash
        static string convertHash(string h)
        {
            return Base36(h.Substring(0, 8)) + Base36(h.Substring(8, 8)) + Base36(h.Substring(16, 8)) + Base36(h.Substring(24, 8));
        }
        #endregion

        #region Base36
        static string Base36(string val)
        {
            string result = "";
            ulong value = Convert.ToUInt64(val, 16);

            const int Base = 36;
            const string Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            while (value > 0)
            {
                result = Chars[(int)(value % Base)] + result; // use StringBuilder for better performance
                value /= Base;
            }

            return result.ToLower();
        }
        #endregion
    }
}
