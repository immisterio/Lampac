using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using System.Text;
using System.Web;
using Online;

namespace Lampac.Controllers.LITE
{
    public class FilmixPartner : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fxapi")]
        async public Task<ActionResult> Index(long kinopoisk_id, bool checksearch, string title, string original_title, int postid, int t, int s = -1)
        {
            if (!AppInit.conf.FilmixPartner.enable || string.IsNullOrWhiteSpace(AppInit.conf.Filmix.host))
                return OnError();

            postid = postid == 0 ? await search(kinopoisk_id) : postid;
            if (postid == 0)
                return OnError();

            if (checksearch)
                return Content("data-json=");

            #region video_links
            string memKey = $"fxapi:{postid}:{HttpContext.Connection.RemoteIpAddress}";
            if (!memoryCache.TryGetValue(memKey, out JArray root))
            {
                string XFXTOKEN = await getXFXTOKEN();
                if (string.IsNullOrWhiteSpace(XFXTOKEN))
                    return OnError();

                root = await HttpClient.Get<JArray>($"{AppInit.conf.FilmixPartner.host}/video_links/{postid}", addHeaders: new List<(string name, string val)>()
                {
                    ("X-FX-TOKEN", XFXTOKEN)
                });

                if (root == null || root.Count == 0)
                    return OnError();

                var first = root.First.ToObject<JObject>();
                if (!first.ContainsKey("files") && !first.ContainsKey("seasons"))
                    return OnError();

                memoryCache.Set(memKey, root, DateTime.Now.AddMinutes(20));
            }
            #endregion

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (root.First.ToObject<JObject>().ContainsKey("files"))
            {
                #region Фильм
                foreach (var movie in root)
                {
                    string streansquality = string.Empty;
                    List<(string link, string quality)> streams = new List<(string, string)>();

                    foreach (var file in movie.Value<JArray>("files").OrderByDescending(i => i.Value<int>("quality")))
                    {
                        int q = file.Value<int>("quality");
                        string l = HostStreamProxy(AppInit.conf.FilmixPartner, file.Value<string>("url"));

                        streams.Add((l, $"{q}p"));
                        streansquality += $"\"{$"{q}p"}\":\"" + l + "\",";
                    }

                    streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                    html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + (title ?? original_title) + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + movie.Value<string>("name") + "</div></div>";
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
                    foreach (var translation in root)
                    {
                        foreach (var season in translation.Value<JArray>("seasons"))
                        {
                            int sid = season.Value<int>("season");
                            string sname = $"{sid} сезон";

                            if (!html.Contains(sname))
                            {
                                string link = $"{host}/lite/fxapi?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={sid}";

                                html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + sname + "</div></div></div>";
                                firstjson = false;
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    #region Перевод
                    int indexTranslate = 0;

                    foreach (var translation in root)
                    {
                        foreach (var season in translation.Value<JArray>("seasons"))
                        {
                            if (season.Value<int>("season") == s)
                            {
                                string link = $"{host}/lite/fxapi?postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={indexTranslate}";
                                string active = t == indexTranslate ? "active" : "";

                                indexTranslate++;
                                html += "<div class=\"videos__button selector " + active + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'>" + translation.Value<string>("name") + "</div>";
                                break;
                            }
                        }
                    }

                    html += "</div><div class=\"videos__line\">";
                    #endregion

                    #region Серии
                    foreach (var episode in root[t].Value<JArray>("seasons").FirstOrDefault(i => i.Value<int>("season") == s).Value<JObject>("episodes").ToObject<Dictionary<string, JObject>>().Values)
                    {
                        string streansquality = string.Empty;
                        List<(string link, string quality)> streams = new List<(string, string)>();

                        foreach (var file in episode.Value<JArray>("files").OrderByDescending(i => i.Value<int>("quality")))
                        {
                            int q = file.Value<int>("quality");
                            string l = HostStreamProxy(AppInit.conf.FilmixPartner, file.Value<string>("url"));

                            streams.Add((l, $"{q}p"));
                            streansquality += $"\"{$"{q}p"}\":\"" + l + "\",";
                        }

                        streansquality = "\"quality\": {" + Regex.Replace(streansquality, ",$", "") + "}";

                        int e = episode.Value<int>("episode");
                        html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + s + "\" e=\"" + e + "\" data-json='{\"method\":\"play\",\"url\":\"" + streams[0].link + "\",\"title\":\"" + $"{title ?? original_title} ({e} серия)" + "\", " + streansquality + "}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + $"{e} серия" + "</div></div>";
                        firstjson = false;
                    }
                    #endregion
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        #region search
        async ValueTask<int> search(long kinopoisk_id)
        {
            if (kinopoisk_id == 0)
                return 0;

            string memKey = $"fxapi:search:{kinopoisk_id}";
            if (!memoryCache.TryGetValue(memKey, out int postid))
            {
                string XFXTOKEN = await getXFXTOKEN();
                if (string.IsNullOrWhiteSpace(XFXTOKEN))
                    return 0;

                var root = await HttpClient.Get<JObject>($"{AppInit.conf.FilmixPartner.host}/film/by-kp/{kinopoisk_id}", timeoutSeconds: 8, addHeaders: new List<(string name, string val)>()
                {
                    ("X-FX-TOKEN", XFXTOKEN)
                });

                if (root == null || !root.ContainsKey("id"))
                    return 0;

                postid = root.Value<int>("id"); 

                if (postid > 0)
                    memoryCache.Set(memKey, postid, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 40 : 10));
            }

            return postid;
        }
        #endregion

        #region getXFXTOKEN
        static int userid = 1;

        static string serverip = null;

        async ValueTask<string> getXFXTOKEN()
        {
            if (serverip == null)
            {
                var myip = await HttpClient.Get<JObject>($"{AppInit.conf.FilmixPartner.host}/my_ip");
                if (myip == null || string.IsNullOrWhiteSpace(myip.Value<string>("ip")))
                    return null;

                serverip = myip.Value<string>("ip");
            }

            if (userid > 20_000)
                userid = 1;
            userid++;

            string XNICK = ReverseString(DateTime.Now.ToString("HHmm")) + DateTime.Now.ToString("yyyyMMdd");
            string XSAM = ReverseString(serverip.Replace(".", "")) + DateTime.Now.ToString("HHmm");

            var salt = await HttpClient.Post<JObject>($"{AppInit.conf.FilmixPartner.host}/request-salt", $"key={AppInit.conf.FilmixPartner.APIKEY}", addHeaders: new List<(string name, string val)>()
            {
                ("X-NICK", SHA1(XNICK)),
                ("X-SAM", SHA1(XSAM))
            });

            if (salt == null || string.IsNullOrWhiteSpace(salt.Value<string>("salt")))
                return null;

            string token = SHA1(AppInit.conf.FilmixPartner.APISECRET + AppInit.conf.FilmixPartner.APIKEY + CrypTo.md5(array_sum(serverip) + salt.Value<string>("salt")));

            var xtk = await HttpClient.Post<JObject>($"{AppInit.conf.FilmixPartner.host}/request-token", $"user_name={AppInit.conf.FilmixPartner.user_name}&user_passw={AppInit.conf.FilmixPartner.user_passw}&key={AppInit.conf.FilmixPartner.APIKEY}&token={token}", addHeaders: new List<(string name, string val)>()
            {
                ("User-Id", userid.ToString()),
            });

            if (xtk != null && !string.IsNullOrWhiteSpace(xtk.Value<string>("token")))
                return xtk.Value<string>("token");

            return null;
        }
        #endregion

        #region ReverseString / array_sum / SHA1
        static string ReverseString(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }

        static int array_sum(string s)
        {
            List<int> mass = new List<int>();
            foreach (var num in s.Split("."))
                mass.Add(int.Parse(num));

            return mass.Sum();
        }

        static string SHA1(string IntText)
        {
            using (var sha1 = new System.Security.Cryptography.SHA1Managed())
            {
                var result = sha1.ComputeHash(Encoding.UTF8.GetBytes(IntText));
                return BitConverter.ToString(result).Replace("-", "").ToLower();
            }
        }
        #endregion
    }
}
