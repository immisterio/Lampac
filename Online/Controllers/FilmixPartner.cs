using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text;
using System.Web;
using Online;
using Shared.Model.Online.Filmix;
using Shared.Model.Templates;
using System.Text.Json;
using Shared.Engine.CORE;
using Shared.Model.Online;

namespace Lampac.Controllers.LITE
{
    public class FilmixPartner : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fxapi")]
        async public Task<ActionResult> Index(long kinopoisk_id, bool checksearch, string title, string original_title, int year, int postid, int t, int s = -1)
        {
            var init = AppInit.conf.FilmixPartner;

            if (!init.enable)
                return OnError();

            if (postid == 0)
            {
                var res = await InvokeCache($"fxapi:search:{title}:{original_title}", cacheTime(40, init: init), () => Search(title, original_title, year));
                postid = res.id;

                // платный поиск
                if (!checksearch && postid == 0 && kinopoisk_id > 0)
                    postid = await search(kinopoisk_id);

                if (postid == 0 && res.similars != null)
                    return Content(res.similars);
            }

            if (postid == 0)
                return OnError();

            if (checksearch)
                return Content("data-json=");

            #region video_links
            string memKey = $"fxapi:{postid}:{HttpContext.Connection.RemoteIpAddress}";
            if (!hybridCache.TryGetValue(memKey, out JArray root))
            {
                string XFXTOKEN = await getXFXTOKEN();
                if (string.IsNullOrWhiteSpace(XFXTOKEN))
                    return OnError();

                root = await HttpClient.Get<JArray>($"{init.corsHost()}/video_links/{postid}", headers: httpHeaders(init, HeadersModel.Init("X-FX-TOKEN", XFXTOKEN)));

                if (root == null || root.Count == 0)
                    return OnError();

                var first = root.First.ToObject<JObject>();
                if (!first.ContainsKey("files") && !first.ContainsKey("seasons"))
                    return OnError();

                hybridCache.Set(memKey, root, DateTime.Now.AddHours(1));
            }
            #endregion

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            if (root.First.ToObject<JObject>().ContainsKey("files"))
            {
                #region Фильм
                var mtpl = new MovieTpl(title, original_title, root.Count);

                foreach (var movie in root)
                {
                    var streams = new List<(string link, string quality)>();

                    foreach (var file in movie.Value<JArray>("files").OrderByDescending(i => i.Value<int>("quality")))
                    {
                        int q = file.Value<int>("quality");
                        string l = HostStreamProxy(init, file.Value<string>("url"));

                        streams.Add((l, $"{q}p"));
                    }

                    mtpl.Append(movie.Value<string>("name"), streams[0].link, streamquality: new StreamQualityTpl(streams));
                }

                return Content(mtpl.ToHtml(), "text/html; charset=utf-8");
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
                            string l = HostStreamProxy(init, file.Value<string>("url"));

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


        [HttpGet]
        [Route("lite/fxapi/lowlevel/{*uri}")]
        async public Task<ActionResult> LowlevelApi(string uri)
        {
            var init = AppInit.conf.FilmixPartner;

            if (!init.enable)
                return OnError("disable");

            if (!HttpContext.Request.Headers.TryGetValue("low_passw", out var low_passw) || low_passw.ToString() != init.lowlevel_api_passw)
                return OnError("lowlevel_api");

            string XFXTOKEN = await getXFXTOKEN();
            if (string.IsNullOrWhiteSpace(XFXTOKEN))
                return OnError("XFXTOKEN");

            string json = await HttpClient.Get($"{init.corsHost()}/{uri}", headers: httpHeaders(init, HeadersModel.Init("X-FX-TOKEN", XFXTOKEN)));

            return Content(json, "application/json; charset=utf-8");
        }


        #region search
        async ValueTask<int> search(long kinopoisk_id)
        {
            if (kinopoisk_id == 0)
                return 0;

            string memKey = $"fxapi:search:{kinopoisk_id}";
            if (!hybridCache.TryGetValue(memKey, out int postid))
            {
                string XFXTOKEN = await getXFXTOKEN();
                if (string.IsNullOrWhiteSpace(XFXTOKEN))
                    return 0;

                var root = await HttpClient.Get<JObject>($"{AppInit.conf.FilmixPartner.corsHost()}/film/by-kp/{kinopoisk_id}", headers: httpHeaders(AppInit.conf.FilmixPartner, HeadersModel.Init("X-FX-TOKEN", XFXTOKEN)));

                if (root == null || !root.ContainsKey("id"))
                    return 0;

                postid = root.Value<int>("id"); 

                if (postid > 0)
                    hybridCache.Set(memKey, postid, DateTime.Now.AddDays(20));
                else
                    hybridCache.Set(memKey, postid, DateTime.Now.AddHours(1));
            }

            return postid;
        }


        async public ValueTask<(int id, string similars)> Search(string title, string original_title, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return (0, null);

            var proxyManager = new ProxyManager("filmix", AppInit.conf.Filmix);
            var proxy = proxyManager.Get();

            string uri = $"{AppInit.conf.Filmix.corsHost()}/api/v2/search?story={HttpUtility.UrlEncode(title)}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={AppInit.conf.Filmix.token}&user_dev_vendor=Xiaomi";

            string json = await HttpClient.Get(AppInit.conf.Filmix.cors(uri), timeoutSeconds: 8, proxy: proxy, headers: httpHeaders(AppInit.conf.Filmix));
            if (json == null)
            {
                proxyManager.Refresh();
                return (0, null);
            }

            var root = JsonSerializer.Deserialize<List<SearchModel>>(json);
            if (root == null || root.Count == 0)
                return (0, null);

            var ids = new List<int>();
            var stpl = new SimilarTpl(root.Count);

            string enc_title = HttpUtility.UrlEncode(title);
            string enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in root)
            {
                if (item == null)
                    continue;

                string name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.original_title) ? $"{item.title} / {item.original_title}" : (item.title ?? item.original_title);

                stpl.Append(name, item.year.ToString(), string.Empty, host + $"lite/fxapi?postid={item.id}&title={enc_title}&original_title={enc_original_title}");

                if ((!string.IsNullOrEmpty(title) && item.title?.ToLower() == title.ToLower()) ||
                    (!string.IsNullOrEmpty(original_title) && item.original_title?.ToLower() == original_title.ToLower()))
                {
                    if (item.year == year)
                        ids.Add(item.id);
                }
            }

            if (ids.Count == 1)
                return (ids[0], null);

            return (0, stpl.ToHtml());
        }
        #endregion

        #region getXFXTOKEN
        static int userid = 1;

        static string serverip = null;

        async ValueTask<string> getXFXTOKEN()
        {
            var init = AppInit.conf.FilmixPartner;

            if (serverip == null)
            {
                var myip = await HttpClient.Get<JObject>($"{init.host}/my_ip", headers: httpHeaders(init));
                if (myip == null || string.IsNullOrWhiteSpace(myip.Value<string>("ip")))
                    return null;

                serverip = myip.Value<string>("ip");
            }

            if (userid > 20_000)
                userid = 1;
            userid++;

            string XNICK = ReverseString(DateTime.Now.ToString("HHmm")) + DateTime.Now.ToString("yyyyMMdd");
            string XSAM = ReverseString(serverip.Replace(".", "")) + DateTime.Now.ToString("HHmm");

            var salt = await HttpClient.Post<JObject>($"{init.host}/request-salt", $"key={init.APIKEY}", headers: httpHeaders(init, HeadersModel.Init(
                ("X-NICK", SHA1(XNICK)),
                ("X-SAM", SHA1(XSAM))
            )));

            if (salt == null || string.IsNullOrWhiteSpace(salt.Value<string>("salt")))
                return null;

            string token = SHA1(init.APISECRET + init.APIKEY + CrypTo.md5(array_sum(serverip) + salt.Value<string>("salt")));

            var xtk = await HttpClient.Post<JObject>($"{init.host}/request-token", $"user_name={init.user_name}&user_passw={init.user_passw}&key={init.APIKEY}&token={token}", headers: httpHeaders(init, HeadersModel.Init("User-Id", userid.ToString())));

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
