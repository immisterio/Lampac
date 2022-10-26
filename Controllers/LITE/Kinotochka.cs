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
    public class Kinotochka : BaseController
    {
        [HttpGet]
        [Route("lite/kinotochka")]
        async public Task<ActionResult> Index(string title, string original_title, int year)
        {
            if (year == 0)
                return Content(string.Empty);

            string file = await embed(memoryCache, title, original_title, year);
            if (file == null)
                return Content(string.Empty);

            string html = "<div class=\"videos__line\">";

            file = $"{AppInit.Host(HttpContext)}/proxy/{file}";
            html += "<div class=\"videos__item videos__movie selector focused\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + file + "\",\"title\":\"" + title + "\"}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">По умолчанию</div></div>";

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region embed
        async static ValueTask<string> embed(IMemoryCache memoryCache, string title, string original_title, int year)
        {
            string memKey = $"kinotochka:view:{title}:{original_title}:{year}";

            if (!memoryCache.TryGetValue(memKey, out string file))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Kinotochka.useproxy)
                    proxy = HttpClient.webProxy();

                string search = await HttpClient.Post($"{AppInit.conf.Kinotochka.host}/index.php?do=search", $"do=search&subaction=search&search_start=0&full_search=0&result_from=1&story={HttpUtility.UrlEncode(original_title ?? title)}", timeoutSeconds: 8, proxy: proxy);
                if (search == null)
                    return null;

                string link = null;
                foreach (string row in search.Split("sres-wrap clearfix").Skip(1))
                {
                    if (Regex.Match(row, "<h2>[^\\(]+ \\(([0-9]{4})\\)</h2>").Groups[1].Value == year.ToString())
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

                file = Regex.Match(news, "file:\"(https?://[^\"]+\\.mp4)\"").Groups[1].Value;
                if (string.IsNullOrWhiteSpace(file))
                    return null;

                memoryCache.Set(memKey, file, DateTime.Now.AddMinutes(10));
            }

            return file;
        }
        #endregion
    }
}
