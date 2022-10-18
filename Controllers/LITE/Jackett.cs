using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Web;
using System.Linq;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Lampac.Engine;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.LITE
{
    public class Jackett : BaseController
    {
        [Route("lite/jac")]
        async public Task<ActionResult> Index(string apikey, string title, string original_title, int year, int quality = -1)
        {
            string memkey = $"lite/jac:{title}:{original_title}:{year}";
            if (!memoryCache.TryGetValue(memkey, out JArray results) || quality == -1)
            {
                var root = await HttpClient.Get<JObject>($"{AppInit.Host(HttpContext)}/api/v2.0/indexers/all/results?apikey={apikey}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&year={year}", timeoutSeconds: 8);
                if (root == null || root.Count == 0)
                    return null;

                results = root.GetValue("Results").ToObject<JArray>();
                if (results == null || results.Count == 0)
                    return null;

                memoryCache.Set(memkey, results, DateTime.Now.AddMinutes(10));
            }

            bool firstjson = true;
            string html = string.Empty;

            #region Меню качества
            HashSet<int> qualitys = new HashSet<int>();

            foreach (var item in results)
            {
                var info = item.Value<JObject>("Info");
                if (info != null)
                    qualitys.Add(info.Value<int>("quality"));
            }

            html = "<div class=\"videos__line\">";

            foreach (int q in qualitys.OrderByDescending(i => i))
            {
                string link = $"{AppInit.Host(HttpContext)}/lite/jac?year={year}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&quality={q}";

                string active = q == quality ? "active" : "";

                html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + $"{q}p" + "</div>";
                firstjson = false;
            }

            firstjson = true;
            html += "</div>";
            #endregion

            foreach (var item in results)
            {
                int sid = item.Value<int>("Seeders"), pir = item.Value<int>("Peers"), q = 0;
                string magnet = item.Value<string>("MagnetUri");
                string tracker = item.Value<string>("Tracker");
                string sizeName = null;

                if (string.IsNullOrWhiteSpace(magnet))
                    magnet = item.Value<string>("Link") + $"&apikey={apikey}";

                var info = item.Value<JObject>("Info");
                if (info != null)
                {
                    q = info.Value<int>("quality");
                    sizeName = info.Value<string>("sizeName");

                    if (quality != -1 && quality != q)
                        continue;
                }

                html += "<div class=\"videos__item videos__torrent selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"torrent\",\"Link\":\"" + magnet + "\",\"title\":\"" + (title ?? original_title) + "\"}'><div class=\"videos__torrent-title\">" + item.Value<string>("Title") + $"</div><div class=\"videos__item-title\">Размер {sizeName} / Раздают {sid} / Качают {pir} / {q}p / {tracker}</div></div>";
                firstjson = false;
            }

            return Content(html, "text/html; charset=utf-8");
        }
    }
}
