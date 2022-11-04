using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Linq;

namespace Lampac.Controllers.LITE
{
    public class Kinoprofi : BaseController
    {
        [HttpGet]
        [Route("lite/kinoprofi")]
        async public Task<ActionResult> Index(string title, string original_title, int year)
        {
            if (year == 0 || string.IsNullOrWhiteSpace(title) || !AppInit.conf.Kinoprofi.enable)
                return Content(string.Empty);

            string file = await embed(memoryCache, title, year);
            if (file == null)
                return Content(string.Empty);

            string html = "<div class=\"videos__line\">";

            file = $"{AppInit.Host(HttpContext)}/proxy/{file}";
            html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + (title ?? original_title) + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async static ValueTask<string> embed(IMemoryCache memoryCache, string title, int year)
        {
            string memKey = $"kinoprofi:view:{title}:{year}";

            if (!memoryCache.TryGetValue(memKey, out string file))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Kinoprofi.useproxy)
                    proxy = HttpClient.webProxy();

                string search = await HttpClient.Get($"{AppInit.conf.Kinoprofi.host}/search/f:{HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                if (search == null)
                    return null;

                string keyid = null;
                foreach (string row in Regex.Replace(search, "[\n\r\t]+", "").Split("sh-block ns").Skip(1))
                {
                    if (Regex.Match(row, "itemprop=\"name\" content=\"([^\"]+)\"").Groups[1].Value.ToLower() != title.ToLower())
                        continue;

                    if (Regex.Match(row, "<b>Год</b> ?<i>([0-9]{4})</i>").Groups[1].Value == year.ToString())
                    {
                        keyid = Regex.Match(row, "href=\"https?://[^/]+/([0-9]+)-[^\"]+\" itemprop=\"url\"").Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(keyid))
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(keyid))
                    return null;

                string session = Regex.Match(search, "var session_id += '([^']+)'").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(session))
                    return null;

                string json = await HttpClient.Post($"{AppInit.conf.Kinoprofi.apihost}/getplay", $"key%5Bid%5D={keyid}&pl_type=movie&session={session}&is_mobile=0&dle_group=5", timeoutSeconds: 8, proxy: proxy);
                if (json == null || !json.Contains(".m3u8"))
                    return null;

                file = Regex.Match(json, "\"hls\":\"(https?:[^\"]+)\"").Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(file))
                    return null;

                memoryCache.Set(memKey, file, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return file;
        }
        #endregion
    }
}
