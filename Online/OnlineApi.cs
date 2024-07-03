using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using System;
using IO = System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Shared.Engine.CORE;
using System.IO;
using Shared.Model.Online;
using Shared.Model.Base;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using Shared.Engine.Online;

namespace Lampac.Controllers
{
    public class OnlineApiController : BaseController
    {
        static OnlineApiController()
        {
            Directory.CreateDirectory("cache/externalids");
        }

        #region online.js
        [HttpGet]
        [Route("online.js")]
        public ActionResult Online()
        {
            var init = AppInit.conf.online;

            string file = FileCache.ReadAllText("plugins/online.js");
            file = file.Replace("{localhost}", host);

            if (init.component != "lampac")
            {
                file = file.Replace("component: 'lampac'", $"component: '{init.component}'");
                file = file.Replace("'lampac', component", $"'{init.component}', component");
                file = file.Replace("window.lampac_plugin", $"window.{init.component}_plugin");
            }

            if (!init.version)
            {
                file = Regex.Replace(file, "version: \\'[^\\']+\\'", "version: ''");
                file = file.Replace("manifst.name, \" v\"", "manifst.name, \" \"");
            }

            file = file.Replace("name: 'Lampac'", $"name: '{init.name}'");
            file = Regex.Replace(file, "description: \\'([^\\']+)?\\'", $"description: '{init.description}'");
            file = Regex.Replace(file, "apn: \\'([^\\']+)?\\'", $"apn: '{init.apn}'");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region lite.js
        [HttpGet]
        [Route("lite.js")]
        public ActionResult Lite()
        {
            return Content(FileCache.ReadAllText("plugins/lite.js").Replace("{localhost}", $"{host}/lite"), contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        #region externalids
        static Dictionary<string, string> externalids = null;

        [Route("externalids")]
        async public Task<ActionResult> Externalids(long id, string imdb_id, long kinopoisk_id, int serial)
        {
            if (id == 0)
                return Content("{}");

            if (externalids == null && IO.File.Exists("cache/externalids/master.json"))
            {
                try
                {
                    externalids = JsonConvert.DeserializeObject<Dictionary<string, string>>(IO.File.ReadAllText("cache/externalids/master.json"));
                }
                catch { }
            }

            if (externalids == null)
                externalids = new Dictionary<string, string>();

            #region getAlloha / getVSDN / getTabus
            async Task<string> getAlloha(string imdb)
            {
                var proxyManager = new ProxyManager("alloha", AppInit.conf.Alloha);
                string json = await HttpClient.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 4, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string> getVSDN(string imdb)
            {
                var proxyManager = new ProxyManager("vcdn", AppInit.conf.VCDN);
                string json = await HttpClient.Get("https://videocdn.tv/api/short?api_token=3i40G5TSECmLF77oAqnEgbx61ZWaOYaE&imdb_id=" + imdb, timeoutSeconds: 4, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kp_id\":\"?([0-9]+)\"?").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string> getTabus(string imdb)
            {
                var proxyManager = new ProxyManager("collaps", AppInit.conf.Collaps);
                string json = await HttpClient.Get("https://api.bhcesh.me/franchise/details?token=eedefb541aeba871dcfc756e6b31c02e&imdb_id=" + imdb.Remove(0, 2), timeoutSeconds: 4, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"kinopoisk_id\":\"?([0-9]+)\"?").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }
            #endregion

            #region get imdb_id
            if (string.IsNullOrWhiteSpace(imdb_id))
            {
                string path = $"cache/externalids/{id}";
                if (IO.File.Exists(path))
                {
                    imdb_id = IO.File.ReadAllText(path);
                }
                else
                {
                    string mkey = $"externalids:locktmdb:{serial}:{id}";
                    if (!memoryCache.TryGetValue(mkey, out _))
                    {
                        memoryCache.Set(mkey, 0 , DateTime.Now.AddHours(1));

                        string cat = serial == 1 ? "tv" : "movie";
                        string json = await HttpClient.Get($"https://api.themoviedb.org/3/{cat}/{id}?api_key=4ef0d7355d9ffb5151e987764708ce96&append_to_response=external_ids", timeoutSeconds: 6);
                        if (!string.IsNullOrWhiteSpace(json))
                        {
                            imdb_id = Regex.Match(json, "\"imdb_id\":\"(tt[0-9]+)\"").Groups[1].Value;
                            if (!string.IsNullOrWhiteSpace(imdb_id))
                                IO.File.WriteAllText(path, imdb_id);
                        }
                    }
                }
            }
            #endregion

            #region get kinopoisk_id
            string kpid = null;

            if (!string.IsNullOrWhiteSpace(imdb_id))
            {
                externalids.TryGetValue(imdb_id, out kpid);

                if (string.IsNullOrEmpty(kpid))
                { 
                    string path = $"cache/externalids/{imdb_id}";
                    if (IO.File.Exists(path))
                    {
                        kpid = IO.File.ReadAllText(path);
                        externalids.TryAdd(imdb_id, kpid);
                    }
                    else if (kinopoisk_id == 0)
                    {
                        string mkey = $"externalids:lockkpid:{imdb_id}";
                        if (!memoryCache.TryGetValue(mkey, out _))
                        {
                            memoryCache.Set(mkey, 0, DateTime.Now.AddDays(1));

                            switch (AppInit.conf.online.findkp ?? "all")
                            {
                                case "alloha":
                                    kpid = await getAlloha(imdb_id);
                                    break;
                                case "vsdn":
                                    kpid = await getVSDN(imdb_id);
                                    break;
                                case "tabus":
                                    kpid = await getTabus(imdb_id);
                                    break;
                                default:
                                    {
                                        var tasks = new Task<string>[] { getVSDN(imdb_id), getAlloha(imdb_id), getTabus(imdb_id) };
                                        await Task.WhenAll(tasks);

                                        kpid = tasks[0].Result ?? tasks[1].Result ?? tasks[2].Result;
                                        break;
                                    }
                            }

                            if (!string.IsNullOrEmpty(kpid))
                            {
                                externalids.TryAdd(imdb_id, kpid);
                                IO.File.WriteAllText(path, kpid);
                            }
                        }
                    }
                }
            }
            #endregion

            return Content($"{{\"imdb_id\":\"{imdb_id}\",\"kinopoisk_id\":\"{(kpid != null ? kpid : kinopoisk_id)}\"}}", "application/json; charset=utf-8");
        }
        #endregion

        #region events
        [HttpGet]
        [Route("lifeevents")]
        public ActionResult LifeEvents(long id, string imdb_id, long kinopoisk_id, int serial, string source)
        {
            string json = null;
            JsonResult error(string msg) => Json(new { accsdb = true, ready = true, online = new string[] { }, msg });

            if (memoryCache.TryGetValue(checkOnlineSearchKey(id, source), out (bool ready, int tasks, string online) res))
            {
                if (res.ready && (res.online == null || !res.online.Contains("\"show\":true")))
                {
                    if (string.IsNullOrEmpty(imdb_id) && 0 >= kinopoisk_id)
                        return error($"Добавьте \"IMDB ID\" {(serial == 1 ? "сериала" : "фильма")} на https://themoviedb.org/{(serial == 1 ? "tv" : "movie")}/{id}/edit?active_nav_item=external_ids");

                    return error($"Не удалось найти онлайн для {(serial == 1 ? "сериала" : "фильма")}");
                }

                string online = res.online?.Replace("{localhost}", host) ?? string.Empty;
                json = "{"+ $"\"ready\":{res.ready.ToString().ToLower()},\"tasks\":{res.tasks},\"online\":[{online}]" + "}";
            }

            return Content(json ?? "{\"ready\":false,\"tasks\":0,\"online\":[]}", contentType: "application/javascript; charset=utf-8");
        }


        [HttpGet]
        [Route("lite/events")]
        async public Task<ActionResult> Events(long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, int serial = -1, bool life = false, string account_email = null)
        {
            var online = new List<(string name, string url, string plugin, int index)>(20);
            bool isanime = original_language == "ja";

            var conf = AppInit.conf;

            if (AppInit.modules != null)
            {
                foreach (var item in AppInit.modules.Where(i => i.online != null))
                {
                    try
                    {
                        if (item.assembly.GetType(item.online) is Type t && t.GetMethod("Events") is MethodInfo m)
                        {
                            var result = (List<(string name, string url, string plugin, int index)>)m.Invoke(null, new object[] { host, id, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, serial, account_email });
                            if (result != null && result.Count > 0)
                                online.AddRange(result);
                        }
                    }
                    catch { }
                }
            }

            void send(string name, BaseSettings init, string plugin = null, string arg_title = null, string arg_url = null)
            {
                if (init.enable && !init.rip)
                {
                    string url = init.overridehost;
                    if (string.IsNullOrEmpty(url))
                        url = "{localhost}/lite/" + (plugin ?? name.ToLower()) + arg_url;

                    if (original_language != null && original_language.Split("|")[0] is "ru" or "ja" or "ko" or "zh" or "cn")
                    {
                        string _p = (plugin ?? name.ToLower());
                        if (_p is "eneyida" or "filmix" or "kinoukr" or "rezka" or "redheadsound" or "kinopub" or "alloha" || (_p == "kodik" && kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id)))
                            url += (url.Contains("?") ? "&" : "?") + "clarification=1";
                    }

                    online.Add(($"{init.displayname ?? name}{arg_title}", url, plugin ?? name.ToLower(), init.displayindex > 0 ? init.displayindex : online.Count));
                }
            }

            if (original_language != null && original_language.Split("|")[0] is "ja" or "ko" or "zh" or "cn")
                send("Kodik", conf.Kodik);

            if (serial == -1 || isanime)
            {
                send("Anilibria", conf.AnilibriaOnline);
                send("Animevost", conf.Animevost);
                send("Animebesst", conf.Animebesst);
                send("AnimeGo", conf.AnimeGo);
                send("AniMedia", conf.AniMedia);
            }

            if (kinopoisk_id > 0 && AppInit.conf.VoKino.enable)
                VoKinoInvoke.SendOnline(AppInit.conf.VoKino, online);

            send("Filmix", conf.Filmix, arg_url: (source == "filmix" ? $"?postid={id}" : ""));
            send("KinoPub", conf.KinoPub, arg_url: (source == "pub" ? $"?postid={id}" : ""));
            send("Filmix", conf.FilmixPartner, "fxapi", arg_url: (source == "filmix" ? $"?postid={id}" : ""));

            send("Alloha", conf.Alloha);
            send("Rezka", conf.Rezka);

            if (kinopoisk_id > 0)
            {
                send("Zetflix", conf.Zetflix);
                send("VDBmovies", conf.VDBmovies, "vdbmovies");
            }

            send("VideoCDN", conf.VCDN, "vcdn");
            send("Kinobase", conf.Kinobase);

            if (serial == -1 || serial == 0)
                send("iRemux", conf.iRemux, "remux");

            send("Voidboost", conf.Voidboost);

            if (kinopoisk_id > 0)
                send("Ashdi (UKR)", conf.Ashdi, "ashdi");

            send("Eneyida (UKR)", conf.Eneyida, "eneyida");

            if (!isanime)
                send("Kinoukr (UKR)", conf.Kinoukr, "kinoukr");

            if (kinopoisk_id > 0)
                send("VideoDB", conf.VideoDB);

            if (serial == -1 || serial == 1)
                send("Seasonvar", conf.Seasonvar);

            if (serial == -1 || serial == 1)
                send("LostfilmHD", conf.Lostfilmhd);

            send("Collaps", conf.Collaps);
            send("HDVB", conf.HDVB);

            if (serial == -1 || serial == 0)
                send("Redheadsound", conf.Redheadsound);

            send("Kinotochka", conf.Kinotochka);

            if ((serial == -1 || (serial == 1 && !isanime)) && kinopoisk_id > 0)
                send("CDNmovies", conf.CDNmovies);

            if (serial == -1 || serial == 0)
                send("IframeVideo", conf.IframeVideo);

            if (!life && conf.litejac)
                online.Add(("Jackett", "{localhost}/lite/jac", "jac", online.Count));

            #region checkOnlineSearch
            bool chos = conf.online.checkOnlineSearch && id > 0;

            if (chos && IO.File.Exists("isdocker"))
            {
                string version = await HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/version", timeoutSeconds: 4, headers: HeadersModel.Init("localrequest", IO.File.ReadAllText("passwd")));
                if (version == null || !version.StartsWith(appversion))
                    chos = false;
            }

            if (chos)
            {
                string memkey = checkOnlineSearchKey(id, source);
                if (!memoryCache.TryGetValue(memkey, out (bool ready, int tasks, string online) cache) || !conf.multiaccess)
                {
                    memoryCache.Set(memkey, cache, DateTime.Now.AddSeconds(20));

                    var tasks = new List<Task>();
                    var links = new ConcurrentBag<(string code, int index, bool work)>();

                    foreach (var o in online)
                        tasks.Add(checkSearch(links, tasks, o.index, o.name, o.url, o.plugin, id, imdb_id, kinopoisk_id, title, original_title, original_language, source, year, serial, life));

                    if (life)
                        return Content("{\"life\":true}", contentType: "application/javascript; charset=utf-8");

                    await Task.WhenAll(tasks);

                    cache.ready = true;
                    cache.tasks = tasks.Count;
                    cache.online = string.Join(",", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code));

                    memoryCache.Set(memkey, cache, DateTime.Now.AddMinutes(5));
                }

                if (life)
                    return Content("{\"life\":true}", contentType: "application/javascript; charset=utf-8");

                return Content($"[{cache.online.Replace("{localhost}", host)}]", contentType: "application/javascript; charset=utf-8");
            }
            #endregion

            string online_result = string.Join(",", online.OrderBy(i => i.index).Select(i => "{\"name\":\"" + i.name + "\",\"url\":\"" + i.url + "\",\"balanser\":\"" + i.plugin + "\"}"));
            return Content($"[{online_result.Replace("{localhost}", host)}]", contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        #region checkSearch
        static string checkOnlineSearchKey(long id, string source) => $"ApiController:checkOnlineSearch:{id}:{source?.Replace("tmdb", "")?.Replace("cub", "")}";

        async Task checkSearch(ConcurrentBag<(string code, int index, bool work)> links, List<Task> tasks, int index, string name, string uri, string plugin,
                               long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, string source, int year, int serial, bool life)
        {
            string srq = uri.Replace("{localhost}", $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}");
            var header = uri.Contains("{localhost}") ? HeadersModel.Init(("xhost", host), ("localrequest", IO.File.ReadAllText("passwd"))) : null;

            string res = await HttpClient.Get($"{srq}{(srq.Contains("?") ? "&" : "?")}id={id}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&original_language={original_language}&source={source}&year={year}&serial={serial}&checksearch=true", timeoutSeconds: 10, headers: header);

            if (string.IsNullOrEmpty(res))
                res = string.Empty;

            bool rch = res.Contains("\"rch\":true");
            bool work = res.Contains("data-json=") || rch;

            string quality = string.Empty;
            string balanser = plugin;

            #region определение качества
            if (work && life)
            {
                foreach (string q in new string[] { "2160", "1080", "720", "480", "360" })
                {
                    if (res.Contains("<!--q:"))
                    {
                        quality = " - " + Regex.Match(res, "<!--q:([^>]+)-->").Groups[1].Value;
                        break;
                    }
                    else if (res.Contains($"\"{q}p\"") || res.Contains($">{q}p<") || res.Contains($"<!--{q}p-->"))
                    {
                        quality = $" - {q}p";
                        break;
                    }
                }

                if (quality == "2160")
                    quality = res.Contains("HDR") ? " - 4K HDR" : " - 4K";

                if (balanser == "filmix")
                    quality = AppInit.conf.Filmix.pro ? quality : " - 480p";

                if (balanser == "alloha")
                    quality = string.IsNullOrEmpty(quality) ? (AppInit.conf.Alloha.m4s ? " ~ 2160p" : " ~ 1080p") : quality;

                if (balanser == "rezka")
                {
                    string rezkaq = !string.IsNullOrEmpty(AppInit.conf.Rezka.login) || !string.IsNullOrEmpty(AppInit.conf.Rezka.cookie) ? " ~ 2160p" : " ~ 720p";
                    quality = string.IsNullOrEmpty(quality) ? rezkaq : quality;
                }

                if (balanser == "collaps")
                    quality = AppInit.conf.Collaps.dash ? " ~ 1080p" : " ~ 720p";

                if (quality == string.Empty)
                {
                    switch (balanser)
                    {
                        case "fxapi":
                        case "filmix":
                        case "kinopub":
                        case "vokino":
                        case "alloha":
                        case "remux":
                        case "ashdi":
                            quality = " ~ 2160p";
                            break;
                        case "videodb":
                        case "kinobase":
                        case "zetflix":
                        case "vcdn":
                        case "eneyida":
                        case "kinoukr":
                        case "hdvb":
                        case "anilibria":
                        case "animedia":
                        case "redheadsound":
                        case "iframevideo":
                        case "animego":
                        case "lostfilmhd":
                        case "vdbmovies":
                            quality = " ~ 1080p";
                            break;
                        case "voidboost":
                        case "animevost":
                        case "animebesst":
                        case "kodik":
                        case "kinotochka":
                            quality = " ~ 720p";
                            break;
                        case "kinokrad":
                        case "kinoprofi":
                        case "seasonvar":
                            quality = " - 480p";
                            break;
                        case "cdnmovies":
                            quality = " - 360p";
                            break;
                        default:
                            break;
                    }

                    if (balanser == "vokino")
                        quality = res.Contains("4K HDR") ? " - 4K HDR" : res.Contains("4K ") ? " - 4K" : quality;
                }
            }
            #endregion

            if (!name.Contains(" - ") && !string.IsNullOrEmpty(quality))
            {
                name = Regex.Replace(name, " ~ .*$", "");
                name += quality;
            }

            links.Add(("{" + $"\"name\":\"{name}\",\"url\":\"{uri}\",\"index\":{index},\"show\":{work.ToString().ToLower()},\"balanser\":\"{balanser}\",\"rch\":{rch.ToString().ToLower()}" + "}", index, work));

            memoryCache.Set(checkOnlineSearchKey(id, source), (links.Count == tasks.Count, tasks.Count, string.Join(",", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code))), DateTime.Now.AddMinutes(5));
        }
        #endregion
    }
}