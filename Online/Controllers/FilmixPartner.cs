using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using Lampac.Engine.CORE;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Text;
using System.Web;
using Online;
using Shared.Model.Online.Filmix;
using Shared.Model.Templates;
using Shared.Engine.CORE;
using Shared.Model.Online;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Lampac.Controllers.LITE
{
    public class FilmixPartner : BaseOnlineController
    {
        [HttpGet]
        [Route("lite/fxapi")]
        async public Task<ActionResult> Index(long kinopoisk_id, bool checksearch, string title, string original_title, int year, int postid, int t = -1, int s = -1, bool rjson = false)
        {
            var init = loadKit(AppInit.conf.FilmixPartner.Clone());
            if (IsBadInitialization(init, out ActionResult action, rch: false))
                return action;

            if (postid == 0)
            {
                var res = await InvokeCache($"fxapi:search:{title}:{original_title}", cacheTime(40, init: init), () => Search(title, original_title, year));

                if (res != null)
                    postid = res.id;

                // платный поиск
                if (!checksearch && postid == 0 && kinopoisk_id > 0)
                    postid = await searchKp(kinopoisk_id);

                if (postid == 0 && res?.similars != null)
                    return ContentTo(rjson ? res.similars.ToJson() : res.similars.ToHtml());
            }

            if (postid == 0)
                return OnError();

            if (checksearch)
                return Content("data-json=");

            string hashKey = $"fxapi:hashfimix:{requestInfo.IP}";
            hybridCache.TryGetValue(hashKey, out string hashfimix);

            #region video_links
            string videoKey = $"fxapi:{postid}:{(string.IsNullOrEmpty(hashfimix) ? requestInfo.IP : "")}";
            if (!hybridCache.TryGetValue(videoKey, out JArray root))
            {
                string XFXTOKEN = await getXFXTOKEN(requestInfo.user_uid);
                if (string.IsNullOrWhiteSpace(XFXTOKEN))
                    return OnError();

                root = await HttpClient.Get<JArray>($"{init.corsHost()}/video_links/{postid}", headers: httpHeaders(init, HeadersModel.Init("X-FX-TOKEN", XFXTOKEN)));

                if (root == null || root.Count == 0)
                    return OnError();

                var first = root.First.ToObject<JObject>();
                if (!first.ContainsKey("files") && !first.ContainsKey("seasons"))
                    return OnError();

                if (string.IsNullOrEmpty(hashfimix))
                {
                    hashfimix = Regex.Match(root.First.ToString().Replace("\\", ""), "/s/([^/]+)/").Groups[1].Value;

                    if (!string.IsNullOrEmpty(hashfimix))
                    {
                        videoKey = $"fxapi:{postid}";
                        hybridCache.Set(hashKey, hashfimix, DateTime.Now.AddHours(1));
                    }
                }

                hybridCache.Set(videoKey, root, DateTime.Now.AddHours(2));
            }
            #endregion

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
                        string url = file.Value<string>("url");
                        if (!string.IsNullOrEmpty(hashfimix))
                            url = Regex.Replace(url, "/s/[^/]+/", $"/s/{hashfimix}/");

                        string l = HostStreamProxy(init, url);

                        streams.Add((l, $"{q}p"));
                    }

                    mtpl.Append(movie.Value<string>("name"), streams[0].link, streamquality: new StreamQualityTpl(streams), vast: init.vast);
                }

                return ContentTo(rjson ? mtpl.ToJson() : mtpl.ToHtml());
                #endregion
            }
            else
            {
                #region Сериал
                if (s == -1)
                {
                    #region Сезоны
                    var tpl = new SeasonTpl(root.Count);
                    var temp_season = new HashSet<int>();

                    foreach (var translation in root)
                    {
                        foreach (var season in translation.Value<JArray>("seasons"))
                        {
                            int sid = season.Value<int>("season");
                            string sname = $"{sid} сезон";

                            if (!temp_season.Contains(sid))
                            {
                                temp_season.Add(sid);
                                string link = $"{host}/lite/fxapi?rjson={rjson}&postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={sid}";
                                tpl.Append(sname, link, sid);
                            }
                        }
                    }

                    return ContentTo(rjson ? tpl.ToJson() : tpl.ToHtml());
                    #endregion
                }
                else
                {
                    #region Перевод
                    int indexTranslate = 0;
                    var vtpl = new VoiceTpl();

                    foreach (var translation in root)
                    {
                        foreach (var season in translation.Value<JArray>("seasons"))
                        {
                            if (season.Value<int>("season") == s)
                            {
                                string link = $"{host}/lite/fxapi?rjson={rjson}&postid={postid}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&s={s}&t={indexTranslate}";
                                bool active = t == indexTranslate;

                                if (t == -1)
                                    t = indexTranslate;

                                vtpl.Append(translation.Value<string>("name"), active, link);
                                break;
                            }
                        }

                        indexTranslate++;
                    }
                    #endregion

                    #region Серии
                    var etpl = new EpisodeTpl();

                    foreach (var episode in root[t].Value<JArray>("seasons").FirstOrDefault(i => i.Value<int>("season") == s).Value<JObject>("episodes").ToObject<Dictionary<string, JObject>>().Values)
                    {
                        List<(string link, string quality)> streams = new List<(string, string)>();

                        foreach (var file in episode.Value<JArray>("files").OrderByDescending(i => i.Value<int>("quality")))
                        {
                            int q = file.Value<int>("quality");
                            string url = file.Value<string>("url");
                            if (!string.IsNullOrEmpty(hashfimix))
                                url = Regex.Replace(url, "/s/[^/]+/", $"/s/{hashfimix}/");

                            string l = HostStreamProxy(init, url);

                            streams.Add((l, $"{q}p"));
                        }

                        int e = episode.Value<int>("episode");
                        etpl.Append($"{e} серия", title ?? original_title, s.ToString(), e.ToString(), streams[0].link, streamquality: new StreamQualityTpl(streams), vast: init.vast);
                    }
                    #endregion

                    if (rjson)
                        return ContentTo(etpl.ToJson(vtpl));

                    return ContentTo(vtpl.ToHtml() + etpl.ToHtml());
                }
                #endregion
            }
        }


        [HttpGet]
        [Route("lite/fxapi/lowlevel/{*uri}")]
        async public Task<ActionResult> LowlevelApi(string uri)
        {
            var init = AppInit.conf.FilmixPartner;

            if (!init.enable)
                return OnError("disable", gbcache: false);

            if (!HttpContext.Request.Headers.TryGetValue("low_passw", out var low_passw) || low_passw.ToString() != init.lowlevel_api_passw)
                return OnError("lowlevel_api", gbcache: false);

            string XFXTOKEN = await getXFXTOKEN();
            if (string.IsNullOrWhiteSpace(XFXTOKEN))
                return OnError("XFXTOKEN", gbcache: false);

            string json = await HttpClient.Get($"{init.corsHost()}/{uri}", headers: httpHeaders(init, HeadersModel.Init("X-FX-TOKEN", XFXTOKEN)));

            return Content(json, "application/json; charset=utf-8");
        }


        #region search
        async ValueTask<int> searchKp(long kinopoisk_id)
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
                    hybridCache.Set(memKey, postid, DateTime.Now.AddDays(1));
            }

            return postid;
        }


        async ValueTask<SearchResult> Search(string title, string original_title, int year)
        {
            if (string.IsNullOrWhiteSpace(title ?? original_title) || year == 0)
                return null;

            var proxyManager = new ProxyManager("filmix", AppInit.conf.Filmix);
            var proxy = proxyManager.Get();

            string uri = $"{AppInit.conf.Filmix.corsHost()}/api/v2/search?story={HttpUtility.UrlEncode(title)}&user_dev_apk=2.0.1&user_dev_id=&user_dev_name=Xiaomi&user_dev_os=11&user_dev_token={AppInit.conf.Filmix.token}&user_dev_vendor=Xiaomi";

            string json = await HttpClient.Get(AppInit.conf.Filmix.cors(uri), timeoutSeconds: 7, proxy: proxy, useDefaultHeaders: false, headers: HeadersModel.Init(
                ("Accept-Encoding", "gzip")
            ));

            if (json == null)
            {
                proxyManager.Refresh();
                return await Search2(title, original_title, year);
            }

            List<SearchModel> root = null;

            try
            {
                root = JsonConvert.DeserializeObject<List<SearchModel>>(json);
            }
            catch { }

            if (root == null || root.Count == 0)
                return await Search2(title, original_title, year);

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
                return new SearchResult() { id = ids[0] };

            return new SearchResult() { similars = stpl };
        }


        async ValueTask<SearchResult> Search2(string? title, string? original_title, int year)
        {
            async ValueTask<List<SearchModel>> gosearch(string? story)
            {
                if (string.IsNullOrEmpty(story))
                    return null;

                string uri = $"https://api.filmix.tv/api-fx/list?search={HttpUtility.UrlEncode(story)}&limit=48";

                string json = await HttpClient.Get(uri, timeoutSeconds: 5);
                if (string.IsNullOrEmpty(json) || !json.Contains("\"status\":\"ok\""))
                    return null;

                List<SearchModel> root = null;

                try
                {
                    root = JsonConvert.DeserializeObject<List<SearchModel>>(json);
                }
                catch { }

                if (root == null || root.Count == 0)
                    return null;

                return root;
            }

            var result = await gosearch(original_title);
            if (result == null)
                result = await gosearch(title);

            if (result == null)
                return default;

            var ids = new List<int>();
            var stpl = new SimilarTpl(result.Count);

            string? enc_title = HttpUtility.UrlEncode(title);
            string? enc_original_title = HttpUtility.UrlEncode(original_title);

            foreach (var item in result)
            {
                if (item == null)
                    continue;

                string? name = !string.IsNullOrEmpty(item.title) && !string.IsNullOrEmpty(item.original_title) ? $"{item.title} / {item.original_title}" : (item.title ?? item.original_title);

                stpl.Append(name, item.year.ToString(), string.Empty, host + $"lite/filmix?postid={item.id}&title={enc_title}&original_title={enc_original_title}");

                if ((!string.IsNullOrEmpty(title) && item.title?.ToLower() == title.ToLower()) ||
                    (!string.IsNullOrEmpty(original_title) && item.original_title?.ToLower() == original_title.ToLower()))
                {
                    if (item.year == year)
                        ids.Add(item.id);
                }
            }

            if (ids.Count == 1)
                return new SearchResult() { id = ids[0] };

            return new SearchResult() { similars = stpl };
        }
        #endregion

        #region getXFXTOKEN
        static long userid = 1;

        static string serverip = null;

        async ValueTask<string> getXFXTOKEN(string uid = null)
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

            if (uid != null)
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    // Преобразуем строку в байты и вычисляем хэш
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(uid));

                    // Преобразуем первые 8 байт хэша в число
                    long result = BitConverter.ToInt64(bytes, 0);
                    userid = Math.Abs(result); // Возвращаем положительное значение
                }
            }

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
