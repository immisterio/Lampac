using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
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
using System.Data;
using System.Collections.Concurrent;
using Shared.Models.Module;
using Newtonsoft.Json.Linq;
using Microsoft.AspNetCore.Http;
using Shared.PlaywrightCore;

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
        [Route("online/js/{token}")]
        public ActionResult Online(string token)
        {
            var init = AppInit.conf.online;

            string file = FileCache.ReadAllText("plugins/online.js");
            file = file.Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

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

            file = file.Replace("return status$1;", "return true;"); // отключение рекламы

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region lite.js
        [HttpGet]
        [Route("lite.js")]
        [Route("lite/js/{token}")]
        public ActionResult Lite(string token)
        {
            string file = FileCache.ReadAllText("plugins/lite.js").Replace("{localhost}", $"{host}/lite");
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion


        #region externalids
        /// <summary>
        /// imdb_id, kinopoisk_id
        /// </summary>
        static ConcurrentDictionary<string, string> externalids = new ConcurrentDictionary<string, string>();

        static DateTime externalids_lastWriteTime = default;

        [Route("externalids")]
        async public Task<ActionResult> Externalids(string id, string imdb_id, long kinopoisk_id, int serial)
        {
            #region load externalids
            if (IO.File.Exists("cache/externalids/master.json"))
            {
                try
                {
                    var lastWriteTime = IO.File.GetLastWriteTime("cache/externalids/master.json");
                    if (lastWriteTime != externalids_lastWriteTime)
                    {
                        externalids_lastWriteTime = lastWriteTime;
                        foreach (var item in JsonConvert.DeserializeObject<Dictionary<string, string>>(IO.File.ReadAllText("cache/externalids/master.json")))
                            externalids.AddOrUpdate(item.Key, item.Value, (k, v) => item.Value);
                    }
                }
                catch { }
            }
            #endregion

            #region KP_
            if (id != null && id.StartsWith("KP_"))
            {
                string _kp = id.Replace("KP_", "");
                foreach (var eid in externalids)
                {
                    if (eid.Value == _kp && !string.IsNullOrEmpty(eid.Key))
                    {
                        imdb_id = eid.Key;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(imdb_id))
                {
                    return Json(new { imdb_id, kinopoisk_id = _kp });
                }
                else
                {
                    string mkey = $"externalids:KP_:{_kp}";
                    if (!memoryCache.TryGetValue(mkey, out string _imdbid))
                    {
                        string json = await HttpClient.Get($"{AppInit.conf.VideoCDN.corsHost()}/api/short?api_token={AppInit.conf.VideoCDN.token}&kinopoisk_id=" + id.Replace("KP_", ""), timeoutSeconds: 5);
                        _imdbid = Regex.Match(json ?? "", "\"imdb_id\":\"(tt[^\"]+)\"").Groups[1].Value;
                        memoryCache.Set(mkey, _imdbid, DateTime.Now.AddHours(8));
                    }

                    return Json(new { imdb_id = _imdbid, kinopoisk_id = id.Replace("KP_", "") });
                }
            }
            #endregion

            #region getAlloha / getVSDN / getTabus
            async Task<string> getAlloha(string imdb)
            {
                var proxyManager = new ProxyManager("alloha", AppInit.conf.Alloha);
                // e4740218af5a5ca67c6210f7fe3842
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
                if (string.IsNullOrEmpty(AppInit.conf.VideoCDN.token))
                {
                    string json = await HttpClient.Get($"https://kinobd.net/api/films/search/imdb_id?q={imdb}", timeoutSeconds: 4);
                    if (json == null)
                        return null;

                    string kpid = Regex.Match(json, "\"kinopoisk_id\":\"?([0-9]+)\"?").Groups[1].Value;
                    if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                        return kpid;

                    return null;
                }
                else
                {
                    var proxyManager = new ProxyManager("vcdn", AppInit.conf.VideoCDN);
                    string json = await HttpClient.Get($"{AppInit.conf.VideoCDN.corsHost()}/api/short?api_token={AppInit.conf.VideoCDN.token}&imdb_id={imdb}", timeoutSeconds: 4, proxy: proxyManager.Get());
                    if (json == null)
                        return null;

                    string kpid = Regex.Match(json, "\"kp_id\":\"?([0-9]+)\"?").Groups[1].Value;
                    if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                        return kpid;

                    return null;
                }
            }

            async Task<string> getTabus(string imdb)
            {
                var proxyManager = new ProxyManager("collaps", AppInit.conf.Collaps);
                string json = await HttpClient.Get("https://api.bhcesh.me/franchise/details?token=d39edcf2b6219b6421bffe15dde9f1b3&imdb_id=" + imdb.Remove(0, 2), timeoutSeconds: 4, proxy: proxyManager.Get());
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
                if (kinopoisk_id > 0)
                {
                    foreach (var eid in externalids)
                    {
                        if (eid.Value == kinopoisk_id.ToString() && !string.IsNullOrEmpty(eid.Key))
                        {
                            imdb_id = eid.Key;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(imdb_id) && long.TryParse(id, out _))
                {
                    string path = $"cache/externalids/{id}_{serial}";
                    if (IO.File.Exists(path))
                    {
                        imdb_id = IO.File.ReadAllText(path);
                    }
                    else
                    {
                        string mkey = $"externalids:locktmdb:{serial}:{id}";
                        if (!memoryCache.TryGetValue(mkey, out _))
                        {
                            memoryCache.Set(mkey, 0, DateTime.Now.AddHours(1));

                            string cat = serial == 1 ? "tv" : "movie";
                            var header = HeadersModel.Init(("localrequest", IO.File.ReadAllText("passwd")));
                            string json = await HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/tmdb/api/3/{cat}/{id}?api_key={AppInit.conf.tmdb.api_key}&append_to_response=external_ids", timeoutSeconds: 5, headers: header);
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                imdb_id = Regex.Match(json, "\"imdb_id\":\"(tt[0-9]+)\"").Groups[1].Value;
                                if (!string.IsNullOrWhiteSpace(imdb_id))
                                    IO.File.WriteAllText(path, imdb_id);
                            }
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

                if (string.IsNullOrEmpty(kpid) || kpid == "0")
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

                            if (!string.IsNullOrEmpty(kpid) && kpid != "0")
                            {
                                if (externalids.ContainsKey(imdb_id))
                                    externalids[imdb_id] = kpid;
                                else
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
        public ActionResult LifeEvents(string memkey, long id, string imdb_id, long kinopoisk_id, int serial)
        {
            string json = null;
            JsonResult error(string msg) => Json(new { accsdb = true, ready = true, online = new string[] { }, msg });

            if (memoryCache.TryGetValue(memkey, out (bool ready, int tasks, string online) res))
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
        async public Task<ActionResult> Events(long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, string rchtype, int serial = -1, bool life = false, bool islite = false, string account_email = null, string uid = null, string token = null)
        {
            var online = new List<(dynamic init, string name, string url, string plugin, int index)>(20);
            bool isanime = original_language == "ja";

            #region fix title
            bool fix_title = false;

            if (title != null && original_language != null && original_language.Split("|")[0] is "ja" or "ko" or "zh" or "cn")
            {
                Regex chineseRegex = new Regex("[\u4E00-\u9FFF]"); // Диапазон для китайских иероглифов
                Regex japaneseRegex = new Regex("[\u3040-\u30FF\uFF66-\uFF9F]"); // Хирагана, катакана и специальные символы
                Regex koreanRegex = new Regex("[\uAC00-\uD7AF]"); // Диапазон для корейских хангыльских символов

                if (chineseRegex.IsMatch(title) || japaneseRegex.IsMatch(title) || koreanRegex.IsMatch(title))
                {
                    var header = HeadersModel.Init(("localrequest", IO.File.ReadAllText("passwd")));
                    var result = await HttpClient.Get<JObject>($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/tmdb/api/3/{(serial == 1 ? "tv" : "movie")}/{id}?api_key={AppInit.conf.tmdb.api_key}&language=en", timeoutSeconds: 4, headers: header);
                    if (result != null)
                    {
                        string _title = serial == 1 ? result.Value<string>("name") : result.Value<string>("title");
                        if (!string.IsNullOrEmpty(_title))
                        {
                            title = _title;
                            fix_title = true;
                        }
                    }
                }
            }
            #endregion

            var conf = AppInit.conf;
            var user = requestInfo.user;
            JObject kitconf = await loadKitConf();

            #region modules
            if (AppInit.modules != null)
            {
                var args = new OnlineEventsModel(id, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, rchtype, serial, life, islite, account_email, uid, token);

                foreach (var mod in AppInit.modules.Where(i => i.online != null))
                {
                    try
                    {
                        if (mod.assembly.GetType(mod.NamespacePath(mod.online)) is Type t)
                        {
                            void invk(object result)
                            {
                                if (result == null)
                                    return;

                                if (result is List<(string name, string url, string plugin, int index)> list)
                                {
                                    if (list != null && list.Count > 0)
                                    {
                                        foreach (var r in list)
                                            online.Add((null, r.name, r.url, r.plugin, r.index));
                                    }
                                }
                            }

                            if (mod.version >= 3)
                            {
                                if (t.GetMethod("Invoke") is MethodInfo e)
                                    invk(e.Invoke(null, new object[] { HttpContext, memoryCache, requestInfo, host, args }));

                                if (t.GetMethod("InvokeAsync") is MethodInfo es)
                                    invk(await (Task<List<(string, string, string, int)>>)es.Invoke(null, new object[] { HttpContext, memoryCache, requestInfo, host, args }));
                            }
                            else
                            {
                                if (t.GetMethod("Events") is MethodInfo e)
                                    invk(e.Invoke(null, new object[] { host, id, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, serial, account_email }));

                                if (t.GetMethod("EventsAsync") is MethodInfo es)
                                    invk(await (Task<List<(string, string, string, int)>>)es.Invoke(null, new object[] { HttpContext, memoryCache, host, id, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, serial, account_email }));
                            }
                        }
                    }
                    catch (Exception ex) { Console.WriteLine($"Modules {mod.NamespacePath(mod.online)}: {ex.Message}\n\n"); }
                }
            }
            #endregion

            #region send
            void send(BaseSettings _init, string plugin = null, string name = null, string arg_title = null, string arg_url = null, string rch_access = null, BaseSettings myinit = null)
            {
                var init = myinit != null ? _init : loadKit(_init, kitconf);
                bool enable = init.enable && !init.rip;
                if (!enable)
                    return;

                if (init.rhub && !init.rhub_fallback)
                {
                    if (rch_access != null && rchtype != null) 
                    {
                        enable = rch_access.Contains(rchtype);
                        if (enable && init.rhub_geo_disable != null)
                        {
                            if (requestInfo.Country != null && init.rhub_geo_disable.Contains(requestInfo.Country))
                                enable = false;
                        }
                    }
                }

                if (init.geo_hide != null)
                {
                    if (requestInfo.Country != null && init.geo_hide.Contains(requestInfo.Country))
                        enable = false;
                }

                if (enable)
                {
                    if (init.group_hide)
                    {
                        if (init.group > 0)
                        {
                            if (user == null || init.group > user.group)
                                return;
                        }
                        else if (AppInit.conf.accsdb.enable)
                        {
                            if (user == null && string.IsNullOrEmpty(AppInit.conf.accsdb.premium_pattern))
                                return;
                        }
                    }

                    string url = string.Empty;

                    if (string.IsNullOrEmpty(init.overridepasswd))
                    {
                        url = init.overridehost;
                        if (string.IsNullOrEmpty(url) && init.overridehosts != null && init.overridehosts.Length > 0)
                            url = init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];
                    }

                    string displayname = init.displayname ?? name ?? init.plugin;

                    if (!string.IsNullOrEmpty(url))
                    {
                        if (plugin == "collaps-dash")
                        {
                            displayname = displayname.Replace("- 720p", "- 1080p");
                            url = url.Replace("/collaps", "/collaps-dash");
                        }
                    }
                    else {
                        url = "{localhost}/lite/" + (plugin ?? (init.plugin ?? name).ToLower()) + arg_url;
                    }

                    if (original_language != null && original_language.Split("|")[0] is "ru" or "ja" or "ko" or "zh" or "cn")
                    {
                        string _p = (plugin ?? (init.plugin ?? name).ToLower());
                        if (_p is "filmix" or "filmixtv" or "fxapi" or "kinoukr" or "rezka" or "rhsprem" or "redheadsound" or "kinopub" or "alloha" or "lumex" or "vcdn" or "videocdn" or "fancdn" or "redheadsound" or "kinotochka" or "remux") // || (_p == "kodik" && kinopoisk_id == 0 && string.IsNullOrEmpty(imdb_id))
                            url += (url.Contains("?") ? "&" : "?") + "clarification=1";
                    }

                    online.Add((myinit, $"{displayname}{arg_title}", url, (plugin ?? init.plugin ?? name).ToLower(), init.displayindex > 0 ? init.displayindex : online.Count));
                }
            }
            #endregion

            if (original_language != null && original_language.Split("|")[0] is "ja" or "ko" or "zh" or "cn")
                send(conf.Kodik);

            if (serial == -1 || isanime)
            {
                send(conf.AnilibriaOnline, "anilibria", "Anilibria");
                send(conf.AnimeLib);
                send(conf.Animevost, rch_access: "apk,cors");
                send(conf.MoonAnime);
                send(conf.Animebesst, rch_access: "apk");
                send(conf.AnimeGo);
                send(conf.AniMedia);
            }

            #region VoKino
            {
                var myinit = loadKit(conf.VoKino, kitconf , (j, i, c) => 
                {
                    if (j.ContainsKey("online"))
                        i.online = c.online;

                    return i;
                });

                if (kinopoisk_id > 0 && myinit.enable)
                {
                    if (AppInit.conf.accsdb.enable)
                    {
                        if (user != null)
                        {
                            if (myinit.group > user.group && myinit.group_hide) { }
                            else
                                VoKinoInvoke.SendOnline(myinit, online);
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(AppInit.conf.accsdb.premium_pattern))
                                VoKinoInvoke.SendOnline(myinit, online);
                        }
                    }
                    else
                    {
                        if (myinit.group > 0 && myinit.group_hide && (user == null || myinit.group > user.group)) { }
                        else
                            VoKinoInvoke.SendOnline(myinit, online);
                    }
                }
            }
            #endregion

            #region Filmix
            {
                var myinit = loadKit(conf.Filmix, kitconf, (j, i, c) => 
                { 
                    if (j.ContainsKey("pro"))
                        i.pro = c.pro; 
                    return i; 
                });

                send(myinit, arg_url: (source == "filmix" ? $"?postid={id}" : ""), myinit: myinit, rch_access: "apk");
            }

            send(conf.FilmixTV, "filmixtv", arg_url: (source == "filmix" ? $"?postid={id}" : ""));
            send(conf.FilmixPartner, "fxapi", "Filmix", arg_url: (source == "filmix" ? $"?postid={id}" : ""));
            #endregion

            #region KinoPub
            send(conf.KinoPub, arg_url: (source == "pub" ? $"?postid={id}" : ""));

            {
                var myinit = loadKit(conf.Alloha, kitconf , (j, i, c) => 
                { 
                    if (j.ContainsKey("m4s"))
                        i.m4s = c.m4s;
                    return i; 
                });

                send(myinit, myinit: myinit);
            }
            #endregion

            #region Rezka
            {
                var rezka = loadKit(conf.RezkaPrem, kitconf , (j, i, c) => 
                {
                    if (j.ContainsKey("premium"))
                        i.premium = c.premium; 
                    return i; 
                });

                send(rezka, "rhsprem", "HDRezka", myinit: rezka);

                if (!rezka.enable)
                {
                    var myinit = await loadKit(conf.Rezka, (j, i, c) =>
                    {
                        if (j.ContainsKey("premium"))
                            i.premium = c.premium;
                        return i;
                    });

                    send(myinit, myinit: myinit);
                }
            }
            #endregion

            send(conf.Mirage);

            if (serial == -1 || serial == 0)
            {
                if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Kinobase.overridehost))
                    send(conf.Kinobase);
            }

            send(conf.VideoCDN);

            if (Firefox.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Lumex.overridehost))
                send(conf.Lumex);

            if (kinopoisk_id > 0)
            {
                if (PlaywrightBrowser.Status == PlaywrightStatus.NoHeadless || !string.IsNullOrEmpty(conf.VideoDB.overridehost))
                    send(conf.VideoDB);

                if (PlaywrightBrowser.Status == PlaywrightStatus.NoHeadless || !string.IsNullOrEmpty(conf.VDBmovies.overridehost))
                    send(conf.VDBmovies);

                if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Zetflix.overridehost))
                    send(conf.Zetflix);

                if (PlaywrightBrowser.Status == PlaywrightStatus.NoHeadless || !string.IsNullOrEmpty(conf.FanCDN.overridehost))
                    send(conf.FanCDN);
            }

            send(conf.Videoseed);
            send(conf.Vibix, rch_access: "apk,cors");

            if (serial == -1 || serial == 0)
                send(conf.iRemux, "remux");

            #region PidTor
            if (conf.PidTor.enable)
            {
                if ((conf.PidTor.torrs != null && conf.PidTor.torrs.Length > 0) || (conf.PidTor.auth_torrs != null && conf.PidTor.auth_torrs.Count > 0) || AppInit.modules.FirstOrDefault(i => i.dll == "TorrServer.dll" && i.enable) != null)
                {
                    void psend()
                    {
                        if (conf.PidTor.group > 0 && conf.PidTor.group_hide)
                        {
                            if (user == null || conf.PidTor.group > user.group)
                                return;
                        }

                        online.Add((null, $"{conf.PidTor.displayname ?? "Pid̶Tor"}", "{localhost}/lite/pidtor", "pidtor", conf.PidTor.displayindex > 0 ? conf.PidTor.displayindex : online.Count));
                    }

                    psend();
                }
            }
            #endregion

            if (kinopoisk_id > 0)
                send(conf.Ashdi, "ashdi", "Ashdi (Украинский)");

            send(conf.Eneyida, "eneyida", "Eneyida (Украинский)");

            if (!isanime)
                send(conf.Kinoukr, "kinoukr", "Kinoukr (Украинский)", rch_access: "apk,cors");

            #region Collaps
            {
                var myinit = loadKit(conf.Collaps, kitconf, (j, i, c) => 
                {
                    if (j.ContainsKey("dash"))
                        i.dash = c.dash;
                    if (j.ContainsKey("two"))
                        i.two = c.two;
                    return i; 
                });

                if (myinit.two && !myinit.dash)
                    send(myinit, "collaps-dash", "Collaps (dash)", rch_access: "apk");

                send(myinit, "collaps", $"Collaps ({(myinit.dash ? "dash" : "hls")})", rch_access: "apk", myinit: myinit);
            }
            #endregion

            if (serial == -1 || serial == 0)
                send(conf.Redheadsound, rch_access: "apk");

            if (kinopoisk_id > 0)
                send(conf.HDVB);

            send(conf.Kinotochka, rch_access: "apk,cors");

            if ((serial == -1 || (serial == 1 && !isanime)) && kinopoisk_id > 0)
                send(conf.CDNmovies, rch_access: "apk,cors");

            if (serial == -1 || serial == 0)
                send(conf.IframeVideo);

            if (kinopoisk_id > 0 && (serial == -1 || serial == 0))
                send(conf.CDNvideohub, "cdnvideohub", "VideoHUB", rch_access: "apk,cors");

            if (!life && conf.litejac)
                online.Add((null, "Торренты", "{localhost}/lite/jac", "jac", 200));

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
                string memkey = CrypTo.md5($"checkOnlineSearch:{id}:{serial}:{source?.Replace("tmdb", "")?.Replace("cub", "")}:{online.Count}:{(IsKitConf ? requestInfo.user_uid : null)}");

                if (!memoryCache.TryGetValue(memkey, out (bool ready, int tasks, string online) cache) || !conf.multiaccess)
                {
                    memoryCache.Set(memkey, cache, DateTime.Now.AddSeconds(20));

                    var tasks = new List<Task>();
                    var links = new List<(string code, int index, bool work)>();

                    foreach (var o in online)
                        tasks.Add(checkSearch(memkey, links, tasks, o.init, o.index, o.name, o.url, o.plugin, id, imdb_id, kinopoisk_id, title, original_title, original_language, source, year, serial, life, rchtype));

                    if (life)
                        return Json(new { life = true, memkey, title = (fix_title ? title : null) });

                    await Task.WhenAll(tasks).ConfigureAwait(false);

                    cache.ready = true;
                    cache.tasks = tasks.Count;
                    cache.online = string.Join(",", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code));

                    memoryCache.Set(memkey, cache, DateTime.Now.AddMinutes(5));
                }

                if (life)
                    return Json(new { life = true, memkey });

                return Content($"[{cache.online.Replace("{localhost}", host)}]", contentType: "application/javascript; charset=utf-8");
            }
            #endregion

            string online_result = string.Join(",", online.OrderBy(i => i.index).Select(i => "{\"name\":\"" + i.name + "\",\"url\":\"" + i.url + "\",\"balanser\":\"" + i.plugin + "\"}"));
            return Content($"[{online_result.Replace("{localhost}", host)}]", contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        #region checkSearch
        async Task checkSearch(string memkey, List<(string code, int index, bool work)> links, List<Task> tasks, dynamic init, int index, string name, string uri, string plugin,
                               long id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, string source, int year, int serial, bool life, string rchtype)
        {
            try
            {
                string srq = uri.Replace("{localhost}", $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}");
                var header = uri.Contains("{localhost}") ? HeadersModel.Init(("xhost", host), ("xscheme", HttpContext.Request.Scheme), ("localrequest", IO.File.ReadAllText("passwd"))) : null;

                string checkuri = $"{srq}{(srq.Contains("?") ? "&" : "?")}id={id}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&original_language={original_language}&source={source}&year={year}&serial={serial}&rchtype={rchtype}&checksearch=true";
                string res = await HttpClient.Get(AccsDbInvk.Args(checkuri, HttpContext), timeoutSeconds: 10, headers: header).ConfigureAwait(false);

                if (string.IsNullOrEmpty(res))
                    res = string.Empty;

                bool rch = res.Contains("\"rch\":true");
                bool work = res.Contains("data-json=") || rch;

                string quality = string.Empty;
                string balanser = plugin.Contains("/") ? plugin.Split("/")[1] : plugin;

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

                    if (init != null)
                    {
                        if (balanser == "filmix")
                        {
                            if (!init.pro)
                                quality = string.IsNullOrEmpty(init.token) ? " - 480p" : " - 720p";
                        }

                        if (balanser == "alloha")
                            quality = string.IsNullOrEmpty(quality) ? (init.m4s ? " ~ 2160p" : " ~ 1080p") : quality;

                        if (balanser == "rezka" || balanser == "rhs")
                        {
                            string rezkaq = init.premium ? " ~ 2160p" : " ~ 720p";
                            quality = string.IsNullOrEmpty(quality) ? rezkaq : quality;
                        }

                        if (balanser == "collaps")
                            quality = init.dash ? " ~ 1080p" : " ~ 720p";
                    }

                    if (quality == string.Empty)
                    {
                        switch (balanser)
                        {
                            case "fxapi":
                            case "filmix":
                            case "filmixtv":
                            case "kinopub":
                            case "vokino":
                            case "alloha":
                            case "remux":
                            case "pidtor":
                            case "rhsprem":
                            case "animelib":
                            case "mirage":
                                quality = " ~ 2160p";
                                break;
                            case "videodb":
                            case "kinobase":
                            case "zetflix":
                            case "vcdn":
                            case "videocdn":
                            case "lumex":
                            case "vibix":
                            case "videoseed":
                            case "eneyida":
                            case "kinoukr":
                            case "ashdi":
                            case "hdvb":
                            case "anilibria":
                            case "animedia":
                            case "redheadsound":
                            case "iframevideo":
                            case "animego":
                            case "lostfilmhd":
                            case "vdbmovies":
                            case "collaps-dash":
                            case "fancdn":
                            case "cdnvideohub":
                            case "moonanime":
                                quality = " ~ 1080p";
                                break;
                            case "voidboost":
                            case "animevost":
                            case "animebesst":
                            case "kodik":
                            case "kinotochka":
                            case "rhs":
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

                if (!name.Contains(" - ") && AppInit.conf.online.showquality && !string.IsNullOrEmpty(quality))
                {
                    name = Regex.Replace(name, " ~ .*$", "");
                    name += quality;
                }

                links.Add(("{" + $"\"name\":\"{name}\",\"url\":\"{uri}\",\"index\":{index},\"show\":{work.ToString().ToLower()},\"balanser\":\"{plugin}\",\"rch\":{rch.ToString().ToLower()}" + "}", index, work));

                memoryCache.Set(memkey, (links.Count == tasks.Count, tasks.Count, string.Join(",", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code))), DateTime.Now.AddMinutes(5));
            }
            catch (Exception ex) { Console.WriteLine("checkSearch: " + ex.ToString()); }
        }
        #endregion
    }
}