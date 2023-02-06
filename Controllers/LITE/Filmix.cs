using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Web;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.Filmix;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.LITE
{
    public class Filmix : BaseController
    {
        #region filmixpro
        [HttpGet]
        [Route("lite/filmixpro")]
        async public Task<ActionResult> Pro()
        {
            var token_request = await HttpClient.Get<JObject>($"{AppInit.conf.Filmix.host}/api/v2/token_request?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_vendor=Xiaomi&user_dev_token=");

            string html = "1. Откройте <a href='https://filmix.ac/consoles'>https://filmix.ac/consoles</a> <br>";
            html += $"2. Введите код <b>{token_request.Value<string>("user_code")}</b><br>";
            html += $"<br><br>В init.conf<br>";
            html += $"1. Укажите token <b>{token_request.Value<string>("code")}</b><br>";
            html += $"2. Измените \"pro\": false, на \"pro\": true, если у вас PRO аккаунт</b>";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion

        [HttpGet]
        [Route("lite/filmix")]
        async public Task<ActionResult> Index(string title, string original_title, int clarification, int year, int postid, int t, int s = -1)
        {
            if (!AppInit.conf.Filmix.enable)
                return Content(string.Empty);

            postid = postid == 0 ? await search(title, original_title, clarification, year) : postid;
            if (postid == 0)
                return Content(string.Empty);

            string memKey = $"filmix:{postid}";
            if (!memoryCache.TryGetValue(memKey, out RootObject root))
            {
                root = await HttpClient.Get<RootObject>($"{AppInit.conf.Filmix.host}/api/v2/post/{postid}?user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={AppInit.conf.Filmix.token}&user_dev_vendor=Xiaomi", timeoutSeconds: 8, IgnoreDeserializeObject: true, useproxy: AppInit.conf.Filmix.useproxy);
                if (root?.player_links == null)
                    return Content(string.Empty);

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 10 : 5));
            }

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (root.player_links.movie != null && root.player_links.movie.Count > 0)
            {
                #region Фильм
                if (root.player_links.movie.Count == 1 && root.player_links.movie[0].translation.ToLower().StartsWith("заблокировано "))
                    return Content(string.Empty);

                foreach (var v in root.player_links.movie)
                {
                    string link = null;
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (int q in new int[] { 2160, 1440, 1080, 720, 480, 360 })
                    {
                        if (!v.link.Contains($"{q},"))
                            continue;

                        if (string.IsNullOrWhiteSpace(AppInit.conf.Filmix.token) && q > 480)
                            continue;

                        if (!AppInit.conf.Filmix.pro && q > 720)
                            continue;

                        string l = Regex.Replace(v.link, "_\\[[0-9,]+\\]\\.mp4", $"_{q}.mp4");
                        l = HostStreamProxy(AppInit.conf.Filmix.streamproxy, l);

                        if (link == null)
                            link = l;

                        streams.Add((l, $"{q}p"));
                        streansquality += $"\"{$"{q}p"}\":\"" + l + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + v.translation + "</div></div>";
                    firstjson = false;
                }
                #endregion
            }
            else
            {
                #region Сериал
                firstjson = true;

                if (s == -1)
                {
                    #region Сезоны
                    foreach (var season in root.player_links.playlist)
                    {
                        string link = $"{host}/lite/filmix?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={season.Key}";

                        html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + $"{season.Key} сезон" + "</div></div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                else
                {
                    #region Перевод
                    int indexTranslate = 0;

                    foreach (var translation in root.player_links.playlist[s.ToString()])
                    {
                        string link = $"{host}/lite/filmix?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={indexTranslate}";
                        string active = t == indexTranslate ? "active" : "";

                        indexTranslate++;
                        html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation.Key + "</div>";
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    #region Серии
                    foreach (var episode in root.player_links.playlist[s.ToString()].ElementAt(t).Value)
                    {
                        string streansquality = string.Empty;
                        List<(string link, string quality)> streams = new List<(string, string)>();

                        foreach (int lq in episode.Value.qualities.OrderByDescending(i => i))
                        {
                            if (string.IsNullOrWhiteSpace(AppInit.conf.Filmix.token) && lq > 480)
                                continue;

                            if (!AppInit.conf.Filmix.pro && lq > 720)
                                continue;

                            string l = episode.Value.link.Replace("_%s.mp4", $"_{lq}.mp4");
                            l = HostStreamProxy(AppInit.conf.Filmix.streamproxy, l);

                            streams.Add((l, $"{lq}p"));
                            streansquality += $"\"{lq}p\":\"" + l + "\",";
                        }

                        streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + episode.Key + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({episode.Key} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{episode.Key} серия" + "</div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region search
        async ValueTask<int> search(string title, string original_title, int clarification, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return 0;

            string memKey = $"filmix:search:{title}:{original_title}:{clarification}:{year}";
            if (!memoryCache.TryGetValue(memKey, out JArray root))
            {
                root = await HttpClient.Get<JArray>($"{AppInit.conf.Filmix.host}/api/v2/search?story={HttpUtility.UrlEncode(clarification == 1 ? title : (original_title ?? title))}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token=&user_dev_vendor=Xiaomi", timeoutSeconds: 8, useproxy: AppInit.conf.Filmix.useproxy);
                if (root == null || root.Count == 0)
                    return 0;

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            int reservedid = 0;
            foreach (var item in root)
            {
                if (!string.IsNullOrWhiteSpace(title) && item.Value<string>("title").ToLower() == title.ToLower())
                {
                    reservedid = item.Value<int>("id");
                    if (item.Value<int>("year") == year)
                        return reservedid;
                }

                if (!string.IsNullOrWhiteSpace(original_title) && item.Value<string>("original_title")?.ToLower() == original_title.ToLower())
                {
                    reservedid = item.Value<int>("id");
                    if (item.Value<int>("year") == year)
                        return reservedid;
                }
            }

            return reservedid;
        }
        #endregion
    }
}
