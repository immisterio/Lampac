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
    public class Kinokrad : BaseController
    {
        [HttpGet]
        [Route("lite/kinokrad")]
        async public Task<ActionResult> Index(string title, int year)
        {
            if (!AppInit.conf.Kinokrad.enable)
                return Content(string.Empty);

            if (year == 0 || string.IsNullOrWhiteSpace(title))
                return Content(string.Empty);

            string content = await embed(memoryCache, title, year);
            if (content == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = string.Empty;

            foreach (var quality in new List<string> { "1080", "720", "480", "360", "240" })
            {
                string hls = new Regex($"\\[{quality}p\\]" + "(https?://[^\\[\\|\",;\n\r\t ]+.m3u8)").Match(content).Groups[1].Value;
                if (!string.IsNullOrEmpty(hls))
                {
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
        async static ValueTask<string> embed(IMemoryCache memoryCache, string title, int year)
        {
            string memKey = $"kinokrad:view:{title}:{year}";

            if (!memoryCache.TryGetValue(memKey, out string content))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Kinokrad.useproxy)
                    proxy = HttpClient.webProxy();

                string search = await HttpClient.Post($"{AppInit.conf.Kinokrad.host}/index.php?do=search", $"do=search&subaction=search&search_start=1&full_search=0&result_from=1&story={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);
                if (search == null)
                    return null;

                string link = null;
                foreach (string row in search.Split("searchitem").Skip(1))
                {
                    if (Regex.Match(row, "<h3><a [^>]+>[^\\(]+ \\(([0-9]{4})\\)</a></h3>").Groups[1].Value == year.ToString())
                    {
                        link = Regex.Match(row, "href=\"(https?://[^/]+/[^\"]+\\.html)\"").Groups[1].Value;
                        if (!string.IsNullOrWhiteSpace(link))
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                    return null;

                string news = await HttpClient.Get(link, timeoutSeconds: 8, proxy: proxy);
                if (news == null)
                    return null;

                content = Regex.Match(news, "var filmSource ([^\n\r]+)").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(content))
                    return null;

                memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(10));
            }

            return content;
        }
        #endregion
    }
}
