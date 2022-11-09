using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Linq;
using System.Collections.Generic;

namespace Lampac.Controllers.LITE
{
    public class Redheadsound : BaseController
    {
        [HttpGet]
        [Route("lite/redheadsound")]
        async public Task<ActionResult> Index(string title, string original_title, int year)
        {
            if (!AppInit.conf.Redheadsound.enable)
                return Content(string.Empty);

            if (year == 0)
                return Content(string.Empty);

            var content = await embed(original_title ?? title, year);
            if (content.iframe == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = string.Empty;

            foreach (var quality in new List<string> { "1080", "720", "480", "360", "240" })
            {
                string hls = new Regex($"\\[{quality}p\\]" + "/([^\\[\\|\",;\n\r\t ]+.m3u8)").Match(content.iframe).Groups[1].Value;
                if (!string.IsNullOrEmpty(hls))
                {
                    hls = $"{Regex.Match(content.iframeUri, "^(https?://[^/]+)").Groups[1].Value}/{hls}";
                    hls = $"{AppInit.Host(HttpContext)}/proxy/{hls}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + quality + "p</div></div>";
                    firstjson = true;
                }
            }

            if (html == string.Empty)
                return Content(string.Empty);

            return Content("<div class=\"videos__line\">" + html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async ValueTask<(string iframe, string iframeUri)> embed(string title, int year)
        {
            string memKey = $"redheadsound:view:{title}:{year}";

            if (!memoryCache.TryGetValue(memKey, out (string iframe, string iframeUri) cache))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Redheadsound.useproxy)
                    proxy = HttpClient.webProxy();

                string search = await HttpClient.Post($"{AppInit.conf.Redheadsound.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                if (search == null)
                    return (null, null);

                string link = null;
                foreach (string row in search.Split("card d-flex").Skip(1))
                {
                    if (Regex.Match(row, "<span>Год выпуска:</span> ?<a [^>]+>([0-9]{4})</a>").Groups[1].Value == year.ToString())
                    {
                        link = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(link))
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                    return (null, null);

                string news = await HttpClient.Get(link, timeoutSeconds: 8, proxy: proxy);
                if (news == null)
                    return (null, null);

                cache.iframeUri = Regex.Match(news, "<iframe data-src=\"(https?://[^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(cache.iframeUri))
                    return (null, null);

                string iframe = await HttpClient.Get(cache.iframeUri, timeoutSeconds: 8, proxy: proxy);
                if (iframe == null)
                    return (null, null);

                cache.iframe = Regex.Match(iframe, "Playerjs([^\n\r]+)").Groups[1].Value.Replace("\\", "");
                if (string.IsNullOrWhiteSpace(cache.iframe))
                    return (null, null);

                memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 30 : 10));
            }

            return cache;
        }
        #endregion
    }
}
