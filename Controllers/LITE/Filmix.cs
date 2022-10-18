using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Web;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.Filmix;

namespace Lampac.Controllers.LITE
{
    public class Filmix : BaseController
    {
        [HttpGet]
        [Route("lite/filmix")]
        async public Task<ActionResult> Index(string title, string original_title, int serial, int year)
        {
            if (serial != 0 || year == 0)
                return Content(string.Empty);

            var url = await search(title ?? original_title, serial, year);
            if (url == null)
                return Content(string.Empty);

            string id = Regex.Match(url, "/([0-9]+)-[^/]+\\.html").Groups[1].Value;
            var root = await HttpClient.Post<RootObject>($"{AppInit.conf.Filmix.host}/api/movies/player_data?t=1844147559201", $"post_id={id}&showfull=true", timeoutSeconds: 8, addHeaders: new List<(string name, string val)>()
            {
                ("cookie", "x-a-key=sinatra; FILMIXNET=1j59ook8hn417n6ufo3ue1pe3a; _ga_GYLWSWSZ3C=GS1.1.1666078393.1.0.1666078393.0.0.0; _ga=GA1.1.1446684338.1666078393; cto_bundle=QMLJ8V9PQlFNNDdLaWMwUVhKYjZDU2lYcnJ0a09jVXdHTGZaRGhndlNNU2F0bUZMZUV3NUxrZjBqNHUlMkY0YzJUQUgzWmFTNUE3Nm5UTUFNUVlTRkhwaXRsUTUyM0t2R1VWeW1EbnpkNkpCYlI4b1RyMDdTWVRWU3MwV05TUTBKbUFOcWN3d0hxJTJGJTJGMlZLWFB6JTJGcXp4bU45S3JudyUzRCUzRA"),
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", AppInit.conf.Filmix.host),
                ("pragma", "no-cache"),
                ("x-requested-with", "XMLHttpRequest"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-origin")
            });

            if (root?.message?.translations?.video == null || root.message.translations.video.Count == 0)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region Фильм
            foreach (var v in root.message.translations.video)
            {
                string stream = v.Value.Replace(":<:bzl3UHQwaWk0MkdXZVM3TDdB", "").Replace(":<:SURhQnQwOEM5V2Y3bFlyMGVI", "").Replace(":<:bE5qSTlWNVUxZ01uc3h0NFFy", "").Replace(":<:Mm93S0RVb0d6c3VMTkV5aE54", "").Replace(":<:MTluMWlLQnI4OXVic2tTNXpU", "");  
                stream = Encoding.UTF8.GetString(Convert.FromBase64String(stream.Remove(0, 2)));

                string link = null;
                string streansquality = string.Empty;
                List<(string link, string quality)> streams = new List<(string, string)>();

                foreach (string q in new string[] { /*"4K UHD", "1080p Ultra\\+", "1080p",*/ "720p", "480p", "360p" })
                {
                    string l = Regex.Match(stream, $"\\[{q}\\](https?://[^\\?,\\[ ]+\\.mp4)").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(l))
                    {
                        if (link == null)
                            link = l.Replace("https:", "http:");

                        streams.Add((l.Replace("https:", "http:"), q.Replace("\\", "")));
                        streansquality += $"\"{q.Replace("\\", "")}\":\"" + l.Replace("https:", "http:") + "\",";
                    }
                }

                streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + v.Key + "</div></div>";
                firstjson = false;
            }
            #endregion

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region search
        async static ValueTask<string> search(string title, int serial, int year)
        {
            if (serial != 0)
                return null;

            var data = new System.Net.Http.StringContent($"scf=fx&story={HttpUtility.UrlEncode($"{title} {year}")}&search_start=0&do=search&subaction=search&years_ot=1902&years_do={DateTime.Today.Year}&kpi_ot=1&kpi_do=10&imdb_ot=1&imdb_do=10&sort_name=&undefined=asc&sort_date=&sort_favorite=&simple=1", Encoding.GetEncoding(1251), "application/x-www-form-urlencoded");
            string search = await HttpClient.Post($"{AppInit.conf.Filmix.host}/engine/ajax/sphinx_search.php", data, encoding: Encoding.UTF8, timeoutSeconds: 8, useproxy: AppInit.conf.Filmix.useproxy, addHeaders: new List<(string name, string val)>()
            {
                ("cache-control", "no-cache"),
                ("dnt", "1"),
                ("origin", AppInit.conf.Filmix.host),
                ("pragma", "no-cache"),
                ("x-requested-with", "XMLHttpRequest"),
                ("sec-fetch-dest", "empty"),
                ("sec-fetch-mode", "cors"),
                ("sec-fetch-site", "same-origin")
            });

            if (search == null)
                return null;

            string url = Regex.Match(search, "itemprop=\"url\" href=\"(https?://[^\"]+)\"").Groups[1].Value;
            if (string.IsNullOrWhiteSpace(url))
                return null;

            return url;
        }
        #endregion
    }
}
