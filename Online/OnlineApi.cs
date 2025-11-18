using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Models.Events;
using Shared.Models.Module;
using Shared.Models.SQL;
using Shared.PlaywrightCore;
using System.Data;
using System.Text;
using IO = System.IO;

namespace Online.Controllers
{
    public class OnlineApiController : BaseController
    {
        #region online.js
        [HttpGet]
        [AllowAnonymous]
        [Route("online.js")]
        [Route("online/js/{token}")]
        public ContentResult Online(string token)
        {
            var init = AppInit.conf.online;
            var apr = init.appReplace ?? InvkEvent.conf?.Controller?.AppReplace?.online?.regex;

            string memKey = $"online.js:{apr?.Count ?? 0}:{InvkEvent.conf?.Controller?.AppReplace?.online?.list?.Count ?? 0}:{init.version}:{init.description}:{init.apn}:{host}:{init.spider}:{init.component}:{init.name}:{init.spiderName}";
            if (!memoryCache.TryGetValue(memKey, out (string file, string filecleaer) cache))
            {
                cache.file = FileCache.ReadAllText("plugins/online.js", saveCache: false)
                    .Replace("{rch_websoket}", FileCache.ReadAllText($"plugins/rch_{AppInit.conf.rch.websoket}.js", saveCache: false));

                string playerinner = FileCache.ReadAllText("plugins/player-inner.js", saveCache: false)
                       .Replace("{useplayer}", (!string.IsNullOrEmpty(AppInit.conf.playerInner)).ToString().ToLower())
                       .Replace("{notUseTranscoding}", (AppInit.conf.transcoding.enable == false).ToString().ToLower());

                #region appReplace
                if (apr != null)
                {
                    foreach (var r in apr)
                    {
                        string val = r.Value;
                        if (val.StartsWith("file:"))
                            val = IO.File.ReadAllText(val.Substring(5));

                        cache.file = Regex.Replace(cache.file, r.Key, val, RegexOptions.IgnoreCase);
                    }
                }

                if (InvkEvent.conf?.Controller?.AppReplace?.online?.list != null)
                {
                    foreach (var r in InvkEvent.conf.Controller.AppReplace.online.list)
                    {
                        string val = r.Value;
                        if (val.StartsWith("file:"))
                            val = IO.File.ReadAllText(val.Substring(5));

                        cache.file = cache.file.Replace(r.Key, val);
                    }
                }
                #endregion

                if (!init.version)
                {
                    cache.file = Regex.Replace(cache.file, "version: \\'[^\\']+\\'", "version: ''")
                                      .Replace("manifst.name, \" v\"", "manifst.name, \" \"");
                }

                if (init.description != "Плагин для просмотра онлайн сериалов и фильмов")
                    cache.file = Regex.Replace(cache.file, "description: \\'([^\\']+)?\\'", $"description: '{init.description}'");

                if (init.apn != null)
                    cache.file = Regex.Replace(cache.file, "apn: \\'([^\\']+)?\\'", $"apn: '{init.apn}'");

                var bulder = new StringBuilder(cache.file);

                if (!init.spider)
                {
                    bulder = bulder.Replace("addSourceSearch('Spider', 'spider');", "")
                                   .Replace("addSourceSearch('Anime', 'spider/anime');", "");
                }

                if (init.component != "lampac")
                {
                    bulder = bulder.Replace("component: 'lampac'", $"component: '{init.component}'")
                                   .Replace("'lampac', component", $"'{init.component}', component")
                                   .Replace("window.lampac_plugin", $"window.{init.component}_plugin");
                }

                if (init.name != "Lampac")
                    bulder = bulder.Replace("name: 'Lampac'", $"name: '{init.name}'");

                if (init.spiderName != "Spider")
                {
                    bulder = bulder.Replace("addSourceSearch('Spider'", $"addSourceSearch('{init.spiderName}'")
                                   .Replace("addSourceSearch('Anime'", $"addSourceSearch('{init.spiderName} - Anime'");
                }

                bulder = bulder
                    .Replace("{invc-rch}", FileCache.ReadAllText("plugins/invc-rch.js", saveCache: false))
                    .Replace("{invc-rch_nws}", FileCache.ReadAllText("plugins/invc-rch_nws.js", saveCache: false))
                    .Replace("{player-inner}", playerinner)
                    .Replace("{localhost}", host);

                cache.file = bulder.ToString();
                cache.filecleaer = cache.file.Replace("{token}", string.Empty);

                if (AppInit.conf.mikrotik == false)
                    memoryCache.Set(memKey, cache, DateTime.Now.AddMinutes(1));
            }

            if (InvkEvent.conf?.Controller?.AppReplace?.online?.eval != null)
            {
                string source = InvkEvent.AppReplace("online", new EventAppReplace(cache.file, token, null, host, requestInfo, HttpContext.Request, hybridCache));
                return Content(source.Replace("{token}", HttpUtility.UrlEncode(token)), "application/javascript; charset=utf-8");
            }

            return Content(token != null ? cache.file.Replace("{token}", HttpUtility.UrlEncode(token)) : cache.filecleaer, "application/javascript; charset=utf-8");
        }
        #endregion

        #region lite.js
        [HttpGet]
        [AllowAnonymous]
        [Route("lite.js")]
        [Route("lite/js/{token}")]
        public ActionResult Lite(string token)
        {
            var sb = new StringBuilder(FileCache.ReadAllText("plugins/lite.js"));

            sb.Replace("{localhost}", $"{host}/lite")
              .Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
        }
        #endregion


        #region externalids
        /// <summary>
        /// imdb_id, kinopoisk_id
        /// </summary>
        static ConcurrentDictionary<string, string> externalids = new ConcurrentDictionary<string, string>();

        static DateTime externalids_lastWriteTime = default, externalids_nextCheck = default;

        [Route("externalids")]
        async public ValueTask<ActionResult> Externalids(string id, string imdb_id, long kinopoisk_id, int serial)
        {
            #region load externalids
            if (IO.File.Exists("data/externalids.json"))
            {
                try
                {
                    if (externalids_nextCheck > DateTime.Now || externalids_nextCheck == default)
                    {
                        externalids_nextCheck = DateTime.Now.AddMinutes(5);
                        var lastWriteTime = IO.File.GetLastWriteTime("data/externalids.json");
                        if (lastWriteTime != externalids_lastWriteTime)
                        {
                            externalids_lastWriteTime = lastWriteTime;
                            foreach (var item in JsonConvert.DeserializeObject<Dictionary<string, string>>(IO.File.ReadAllText("data/externalids.json")))
                                externalids.AddOrUpdate(item.Key, item.Value, (k, v) => item.Value);
                        }
                    }
                }
                catch { }
            }
            #endregion

            #region KP_
            if (id != null && id.StartsWith("KP_"))
            {
                string _kp = id.Substring(0, 3);
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
                    if (!hybridCache.TryGetValue(mkey, out string _imdbid, inmemory: false))
                    {
                        string json = await Http.Get($"https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&kp=" + _kp, timeoutSeconds: 5);
                        _imdbid = Regex.Match(json ?? "", "\"id_imdb\":\"(tt[^\"]+)\"").Groups[1].Value;
                        hybridCache.Set(mkey, _imdbid, DateTime.Now.AddHours(8), inmemory: false);
                    }

                    return Json(new { imdb_id = _imdbid, kinopoisk_id = _kp });
                }
            }
            #endregion

            #region getAlloha / getVSDN / getTabus
            async Task<string> getAlloha(string imdb)
            {
                var proxyManager = new ProxyManager("alloha", AppInit.conf.Alloha);
                string json = await Http.Get("https://api.alloha.tv/?token=04941a9a3ca3ac16e2b4327347bbc1&imdb=" + imdb, timeoutSeconds: 5, proxy: proxyManager.Get());
                if (json == null)
                    return null;

                string kpid = Regex.Match(json, "\"id_kp\":([0-9]+),").Groups[1].Value;
                if (!string.IsNullOrEmpty(kpid) && kpid != "0" && kpid != "null")
                    return kpid;

                return null;
            }

            async Task<string> getVSDN(string imdb)
            {
                if (AppInit.conf.Lumex.spider && AppInit.conf.mikrotik == false)
                {
                    long? res = Lumex.database.FirstOrDefault(i => i.imdb_id == imdb).kinopoisk_id;
                    if (res > 0)
                        return res.ToString();
                }

                if (string.IsNullOrEmpty(AppInit.conf.VideoCDN.token) || string.IsNullOrEmpty(AppInit.conf.VideoCDN.iframehost))
                    return null;

                var proxyManager = new ProxyManager("vcdn", AppInit.conf.VideoCDN);
                string json = await Http.Get($"{AppInit.conf.VideoCDN.iframehost}/api/short?api_token={AppInit.conf.VideoCDN.token}&imdb_id={imdb}", timeoutSeconds: 5, proxy: proxyManager.Get());
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
                string json = await Http.Get("https://api.bhcesh.me/franchise/details?token=d39edcf2b6219b6421bffe15dde9f1b3&imdb_id=" + imdb.Remove(0, 2), timeoutSeconds: 5, proxy: proxyManager.Get());
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
                    string kinopoisk_id_str = kinopoisk_id.ToString();
                    foreach (var eid in externalids)
                    {
                        if (eid.Value == kinopoisk_id_str && !string.IsNullOrEmpty(eid.Key))
                        {
                            imdb_id = eid.Key;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(imdb_id) && long.TryParse(id, out long _testid) && _testid > 0)
                {
                    using (var sqlDb = new ExternalidsContext())
                        imdb_id = sqlDb.imdb.Find($"{id}_{serial}")?.value;

                    if (string.IsNullOrEmpty(imdb_id))
                    {
                        string mkey = $"externalids:locktmdb:{serial}:{id}";
                        if (!hybridCache.TryGetValue(mkey, out _))
                        {
                            hybridCache.Set(mkey, 0, DateTime.Now.AddHours(1));

                            string cat = serial == 1 ? "tv" : "movie";
                            var header = HeadersModel.Init(("localrequest", AppInit.rootPasswd));
                            string json = await Http.Get($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/{cat}/{id}?api_key={AppInit.conf.tmdb.api_key}&append_to_response=external_ids", timeoutSeconds: 5, headers: header);
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                imdb_id = Regex.Match(json, "\"imdb_id\":\"(tt[0-9]+)\"").Groups[1].Value;
                                if (!string.IsNullOrEmpty(imdb_id))
                                {
                                    using (var sqlDb = new ExternalidsContext())
                                    {
                                        sqlDb.Add(new ExternalidsSqlModel()
                                        {
                                            Id = $"{id}_{serial}",
                                            value = imdb_id
                                        });

                                        await sqlDb.SaveChangesLocks();
                                    }
                                }
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
                    using (var sqlDb = new ExternalidsContext())
                    {
                        kpid = sqlDb.kinopoisk.Find(imdb_id)?.value;

                        if (string.IsNullOrEmpty(kpid) && kinopoisk_id == 0)
                        {
                            string mkey = $"externalids:lockkpid:{imdb_id}";
                            if (!hybridCache.TryGetValue(mkey, out _))
                            {
                                hybridCache.Set(mkey, 0, DateTime.Now.AddDays(1));

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
                                            var tasks = new List<Task<string>> { getVSDN(imdb_id), getAlloha(imdb_id), getTabus(imdb_id) };

                                            while (tasks.Count > 0)
                                            {
                                                var completedTask = await Task.WhenAny(tasks);
                                                tasks.Remove(completedTask);

                                                var result = completedTask.Result;
                                                if (result != null)
                                                {
                                                    kpid = result;
                                                    break;
                                                }
                                            }

                                            break;
                                        }
                                }

                                if (!string.IsNullOrEmpty(kpid) && kpid != "0")
                                {
                                    sqlDb.Add(new ExternalidsSqlModel()
                                    {
                                        Id = imdb_id,
                                        value = kpid
                                    });

                                    await sqlDb.SaveChangesLocks();
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            kpid = kpid != null ? kpid : kinopoisk_id.ToString();
            InvkEvent.Externalids(id, ref imdb_id, ref kpid, serial);

            return Content($"{{\"imdb_id\":\"{imdb_id}\",\"kinopoisk_id\":\"{kpid}\"}}", "application/json; charset=utf-8");
        }
        #endregion

        #region WithSearch
        [AllowAnonymous]
        [Route("lite/withsearch")]
        public ActionResult WithSearch()
        {
            if (AppInit.conf.online.with_search == null)
                return ContentTo("[]");

            return Json(AppInit.conf.online.with_search);
        }
        #endregion

        #region spider
        [Route("lite/spider")]
        [Route("lite/spider/anime")]
        async public Task<ActionResult> Spider(string title)
        {
            if (!AppInit.conf.online.spider)
                return ContentTo("{}");

            bool rhub = false;

            var user = requestInfo.user;
            var piders = new List<(string name, string uri, int index)>();

            bool isanime = HttpContext.Request.Path.Value?.EndsWith("/anime") == true;

            #region module
            OnlineModuleEntry.EnsureCache();
            var spiderArgs = new OnlineSpiderModel(title, isanime);

            if (OnlineModuleEntry.onlineModulesCache != null && OnlineModuleEntry.onlineModulesCache.Count > 0)
            {
                void addResult(List<(string name, string url, int index)> result)
                {
                    if (result == null || result.Count == 0)
                        return;

                    foreach (var item in result)
                    {
                        if (string.IsNullOrEmpty(item.name) || string.IsNullOrEmpty(item.url))
                            continue;

                        piders.Add((item.name, item.url, item.index));
                    }
                }

                foreach (var entry in OnlineModuleEntry.onlineModulesCache)
                {
                    try
                    {
                        if (entry.Spider != null)
                        {
                            try
                            {
                                var result = entry.Spider(HttpContext, memoryCache, requestInfo, host, spiderArgs);
                                addResult(result);
                            }
                            catch { }
                        }

                        if (entry.SpiderAsync != null)
                        {
                            try
                            {
                                var result = await entry.SpiderAsync(HttpContext, memoryCache, requestInfo, host, spiderArgs);
                                addResult(result);
                            }
                            catch { }
                        }

                    }
                    catch (Exception ex) { Console.WriteLine($"Modules {entry.mod?.NamespacePath(entry.mod.online)}: {ex.Message}\n\n"); }
                }
            }

            if (spiderArgs.requireRhub)
                rhub = true;
            #endregion

            #region send
            void send(BaseSettings init, string plugin = null)
            {
                if (!init.spider || !init.enable || init.rip)
                    return;

                if (init.geo_hide != null)
                {
                    if (requestInfo.Country != null && init.geo_hide.Contains(requestInfo.Country))
                        return;
                }

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

                if (init.rhub)
                    rhub = true;

                string url = null;
                string displayname = init.displayname ?? init.plugin;

                if (string.IsNullOrEmpty(init.overridepasswd))
                {
                    url = init.overridehost;
                    if (string.IsNullOrEmpty(url) && init.overridehosts != null && init.overridehosts.Length > 0)
                        url = init.overridehosts[Random.Shared.Next(0, init.overridehosts.Length)];
                }

                if (string.IsNullOrEmpty(url))
                    url = $"{host}/lite/" + (plugin ?? init.plugin).ToLower();

                piders.Add((init.displayname ?? init.plugin, $"{url}?title={HttpUtility.UrlEncode(title)}&clarification=1&rjson=true&similar=true", init.displayindex));
            }
            #endregion

            if (isanime)
            {
                send(AppInit.conf.Kodik);
                send(AppInit.conf.AnimeLib);
                send(AppInit.conf.AnilibriaOnline, "anilibria");
                send(AppInit.conf.Animevost);
                send(AppInit.conf.Animebesst);
                send(AppInit.conf.MoonAnime);
                send(AppInit.conf.AnimeGo);
            }

            send(AppInit.conf.Filmix);
            send(AppInit.conf.FilmixTV, "filmixtv");
            send(AppInit.conf.FilmixPartner, "fxapi");

            send(AppInit.conf.Rezka);
            send(AppInit.conf.RezkaPrem, "rhsprem");

            send(AppInit.conf.KinoPub);
            send(AppInit.conf.GetsTV, "getstv-search");
            send(AppInit.conf.Kinobase);
            send(AppInit.conf.Alloha, "alloha-search");
            send(AppInit.conf.Collaps, "collaps-search");
            send(AppInit.conf.VeoVeo, "veoveo-spider");

            if (!string.IsNullOrEmpty(AppInit.conf.VideoCDN.token))
            {
                if (!AppInit.conf.VideoCDN.disable_protection)
                    rhub = true;

                send(AppInit.conf.VideoCDN);
            }

            if (AppInit.conf.Lumex.priorityBrowser == "http" || PlaywrightBrowser.Status != PlaywrightStatus.disabled)
                send(AppInit.conf.Lumex);

            send(AppInit.conf.VDBmovies);
            send(AppInit.conf.HDVB, "hdvb-search");

            if (rhub)
            {
                var rch = new RchClient(HttpContext, host, new BaseSettings() { rhub = true }, requestInfo);
                if (rch.IsNotConnected())
                    return ContentTo(rch.connectionMsg);
            }

            return Json(piders.OrderByDescending(i => i.index).ToDictionary(k => k.name, v => v.uri));
        }
        #endregion


        #region events
        [HttpGet]
        [AllowAnonymous]
        [Route("lifeevents")]
        public ActionResult LifeEvents(string memkey, long id, string imdb_id, long kinopoisk_id, int serial)
        {
            string json = null;
            JsonResult error(string msg) => Json(new { accsdb = true, ready = true, online = new string[] { }, msg });

            List<(string code, int index, bool work)> _links = null;
            if (memoryCache.TryGetValue(memkey, out List<(string code, int index, bool work)> links))
                _links = links.ToList();

            if (_links != null && _links.Count(i => i.code != null) > 0)
            {
                bool ready = _links.Count == _links.Count(i => i.code != null);
                string online = string.Join(",", _links.Where(i => i.code != null).OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code));

                if (ready && !online.Contains("\"show\":true"))
                {
                    if (string.IsNullOrEmpty(imdb_id) && 0 >= kinopoisk_id)
                        return error($"Добавьте \"IMDB ID\" {(serial == 1 ? "сериала" : "фильма")} на https://themoviedb.org/{(serial == 1 ? "tv" : "movie")}/{id}/edit?active_nav_item=external_ids");

                    return error($"Не удалось найти онлайн для {(serial == 1 ? "сериала" : "фильма")}");
                }

                json = "{" + $"\"ready\":{ready.ToString().ToLower()},\"tasks\":{_links.Count},\"online\":[{online.Replace("{localhost}", host)}]" + "}";
            }

            return Content(json ?? "{\"ready\":false,\"tasks\":0,\"online\":[]}", contentType: "application/javascript; charset=utf-8");
        }


        [HttpGet]
        [Route("lite/events")]
        async public ValueTask<ActionResult> Events(string id, string imdb_id, long kinopoisk_id, long tmdb_id, string title, string original_title, string original_language, int year, string source, string rchtype, int serial = -1, bool life = false, bool islite = false, string account_email = null, string uid = null, string token = null)
        {
            var online = new List<(dynamic init, string name, string url, string plugin, int index)>(50);
            bool isanime = original_language is "ja" or "zh";

            #region fix title
            bool fix_title = false;

            if (title != null && original_language != null && original_language.Split("|")[0] is "ja" or "ko" or "zh" or "cn")
            {
                if (long.TryParse(id, out long tmdbid) && tmdbid > 0)
                {
                    Regex chineseRegex = new Regex("[\u4E00-\u9FFF]"); // Диапазон для китайских иероглифов
                    Regex japaneseRegex = new Regex("[\u3040-\u30FF\uFF66-\uFF9F]"); // Хирагана, катакана и специальные символы
                    Regex koreanRegex = new Regex("[\uAC00-\uD7AF]"); // Диапазон для корейских хангыльских символов

                    if (chineseRegex.IsMatch(title) || japaneseRegex.IsMatch(title) || koreanRegex.IsMatch(title))
                    {
                        var header = HeadersModel.Init(("localrequest", AppInit.rootPasswd));
                        var result = await Http.Get<JObject>($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/tmdb/api/3/{(serial == 1 ? "tv" : "movie")}/{tmdbid}?api_key={AppInit.conf.tmdb.api_key}&language=en", timeoutSeconds: 4, headers: header);
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
            }
            #endregion

            var conf = AppInit.conf;
            var user = requestInfo.user;
            JObject kitconf = await loadKitConf();

            #region modules
            OnlineModuleEntry.EnsureCache();

            if (OnlineModuleEntry.onlineModulesCache != null && OnlineModuleEntry.onlineModulesCache.Count > 0)
            {
                var args = new OnlineEventsModel(id, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, rchtype, serial, life, islite, account_email, uid, token);

                foreach (var entry in OnlineModuleEntry.onlineModulesCache)
                {
                    try
                    {
                        #region version >= 3 methods
                        if (entry.Invoke != null)
                        {
                            try
                            {
                                var result = entry.Invoke(HttpContext, memoryCache, requestInfo, host, args);
                                if (result != null && result.Count > 0)
                                {
                                    foreach (var r in result)
                                        online.Add((null, r.name, r.url, r.plugin, r.index));
                                }
                            }
                            catch { }
                        }

                        if (entry.InvokeAsync != null)
                        {
                            try
                            {
                                var result = await entry.InvokeAsync(HttpContext, memoryCache, requestInfo, host, args);
                                if (result != null && result.Count > 0)
                                {
                                    foreach (var r in result)
                                        online.Add((null, r.name, r.url, r.plugin, r.index));
                                }
                            }
                            catch { }
                        }
                        #endregion

                        #region version < 3 legacy methods
                        if (entry.Events != null)
                        {
                            try
                            {
                                long.TryParse(id, out long tmdbid);
                                var result = entry.Events(host, tmdbid, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, serial, account_email);
                                if (result != null && result.Count > 0)
                                {
                                    foreach (var r in result)
                                        online.Add((null, r.name, r.url, r.plugin, r.index));
                                }
                            }
                            catch { }
                        }

                        if (entry.EventsAsync != null)
                        {
                            try
                            {
                                long.TryParse(id, out long tmdbid);
                                var result = await entry.EventsAsync(HttpContext, memoryCache, host, tmdbid, imdb_id, kinopoisk_id, title, original_title, original_language, year, source, serial, account_email);
                                if (result != null && result.Count > 0)
                                {
                                    foreach (var r in result)
                                        online.Add((null, r.name, r.url, r.plugin, r.index));
                                }
                            }
                            catch { }
                        }
                        #endregion
                    }
                    catch (Exception ex) { Console.WriteLine($"Modules {entry.mod?.NamespacePath(entry.mod.online)}: {ex.Message}\n\n"); }
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

                if (enable && init.client_type != null && rchtype != null)
                    enable = init.client_type.Contains(rchtype);

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

            if (original_language != null && original_language.Split("|")[0] is "ja" or "ko" or "zh" or "cn" or "th" or "vi" or "tl")
                send(conf.Kodik);

            if (serial == -1 || isanime)
            {
                send(conf.AniLiberty);
                send(conf.AnilibriaOnline, "anilibria", "Anilibria");
                send(conf.AnimeLib);
                send(conf.Animevost, rch_access: "apk,cors");
                send(conf.Animebesst, rch_access: "apk");
                send(conf.AnimeGo);
                send(conf.AniMedia);
                send(conf.MoonAnime);
            }

            #region VoKino
            if (kinopoisk_id > 0 || source?.ToLower() == "vokino")
            {
                string vid = kinopoisk_id.ToString();
                if (source?.ToLower() == "vokino" && !string.IsNullOrEmpty(id))
                    vid = id;

                var myinit = loadKit(conf.VoKino, kitconf , (j, i, c) => 
                {
                    if (j.ContainsKey("online"))
                        i.online = c.online;

                    return i;
                });

                if (myinit.enable && !string.IsNullOrEmpty(myinit.token))
                {
                    async ValueTask vkino()
                    {
                        if (myinit.rhub || !conf.online.checkOnlineSearch)
                        {
                            VoKinoInvoke.SendOnline(myinit, online, null);
                        }
                        else
                        {
                            if (!hybridCache.TryGetValue($"vokino:view:{vid}", out JObject view))
                            {
                                view = await Http.Get<JObject>($"{myinit.corsHost()}/v2/view/{vid}?token={myinit.token}", timeoutSeconds: 4);
                                if (view != null)
                                    hybridCache.Set($"vokino:view:{vid}", view, cacheTime(20));
                            }

                            if (view != null && view.ContainsKey("online") && view["online"] is JObject onlineObj)
                                VoKinoInvoke.SendOnline(myinit, online, onlineObj);
                        }
                    };

                    if (AppInit.conf.accsdb.enable)
                    {
                        if (user != null)
                        {
                            if (myinit.group > user.group && myinit.group_hide) { }
                            else
                                await vkino();
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(AppInit.conf.accsdb.premium_pattern))
                                await vkino();
                        }
                    }
                    else
                    {
                        if (myinit.group > 0 && myinit.group_hide && (user == null || myinit.group > user.group)) { }
                        else
                            await vkino();
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

                send(myinit, myinit: myinit, rch_access: "apk");
            }

            send(conf.FilmixTV, "filmixtv");
            send(conf.FilmixPartner, "fxapi", "Filmix");
            #endregion

            send(conf.KinoPub);
            send(conf.IptvOnline, "iptvonline", "iptv.online");
            send(conf.GetsTV);

            #region Alloha
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
                var rezka_premium = loadKit(conf.RezkaPrem, kitconf , (j, i, c) => 
                {
                    if (j.ContainsKey("premium"))
                        i.premium = c.premium; 
                    return i; 
                });

                send(rezka_premium, "rhsprem", "HDRezka", myinit: rezka_premium);

                if (rezka_premium.enable == false)
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

            if (PlaywrightBrowser.Status != PlaywrightStatus.disabled)
                send(conf.Mirage);

            if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Kinobase.overridehost) || conf.Kinobase.overridehosts?.Length > 0)
                send(conf.Kinobase);

            if (conf.VDBmovies.rhub || conf.VDBmovies.priorityBrowser == "http" || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.VDBmovies.overridehost) || conf.VDBmovies.overridehosts?.Length > 0)
                send(conf.VDBmovies, rch_access: "apk");

            if (kinopoisk_id > 0)
            {
                if (conf.VideoDB.rhub || conf.VideoDB.priorityBrowser == "http" || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.VideoDB.overridehost) || conf.VideoDB.overridehosts?.Length > 0)
                    send(conf.VideoDB, rch_access: "apk");

                if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Zetflix.overridehost) || conf.Zetflix.overridehosts?.Length > 0)
                    send(conf.Zetflix);
            }

            if (serial == -1 || serial == 0 || !string.IsNullOrEmpty(conf.FanCDN.token) || !string.IsNullOrEmpty(conf.FanCDN.overridehost) || conf.FanCDN.overridehosts?.Length > 0)
            {
                if (conf.FanCDN.rhub || conf.FanCDN.priorityBrowser == "http" || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.FanCDN.overridehost) || conf.FanCDN.overridehosts?.Length > 0)
                    send(conf.FanCDN, rch_access: "apk");
            }

            send(conf.VideoCDN);

            if (conf.Lumex.priorityBrowser == "http" || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Lumex.overridehost) || conf.Lumex.overridehosts?.Length > 0)
                send(conf.Lumex);

            if (conf.Kinogo.rhub || PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Kinogo.overridehost) || conf.Kinogo.overridehosts?.Length > 0)
                send(conf.Kinogo, rch_access: "apk");

            if (kinopoisk_id > 0)
                send(conf.Ashdi, "ashdi", "Ashdi (Украинский)");

            if (!isanime)
                send(conf.Kinoukr, "kinoukr", "Kinoukr (Украинский)", rch_access: "apk,cors");

            send(conf.Eneyida, "eneyida", "Eneyida (Украинский)", rch_access: "apk,cors");

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

                send(myinit, "collaps", $"Collaps ({(myinit.dash ? "DASH" : "HLS")})", rch_access: "apk", myinit: myinit);

                if (myinit.two && !myinit.dash)
                    send(myinit, "collaps-dash", "Collaps (DASH)", rch_access: "apk");
            }
            #endregion

            if (serial == -1 || serial == 0)
            {
                send(conf.Plvideo, "plvideo", "Plvideo", rch_access: "apk,cors");
                send(conf.RutubeMovie, "rutubemovie", "Rutube", rch_access: "apk,cors");
                send(conf.VkMovie, "vkmovie", "VK Видео", rch_access: "apk,cors");
            }

            if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Videoseed.overridehost) || conf.Videoseed.overridehosts?.Length > 0)
                send(conf.Videoseed);

            send(conf.Vibix, rch_access: "apk,cors");
            send(conf.VeoVeo);

            if (serial == -1 || serial == 0)
                send(conf.iRemux, "remux");

            #region PidTor
            if (conf.PidTor.enable)
            {
                if ((conf.PidTor.torrs != null && conf.PidTor.torrs.Length > 0) || (conf.PidTor.auth_torrs != null && conf.PidTor.auth_torrs.Count > 0) || AppInit.modules.FirstOrDefault(i => i.dll == "TorrServer.dll" && i.enable) != null)
                {
                    void psend()
                    {
                        var _conf = InvkEvent.conf.PidTor != null ? conf.PidTor.Clone() : conf.PidTor;

                        InvkEvent.PidTor(new EventPidTor(_conf, requestInfo, hybridCache));

                        if (!_conf.enable)
                            return;

                        if (_conf.group > 0 && _conf.group_hide)
                        {
                            if (user == null || _conf.group > user.group)
                                return;
                        }

                        online.Add((null, $"{_conf.displayname ?? "PidŦor"}", "{localhost}/lite/pidtor", "pidtor", _conf.displayindex > 0 ? _conf.displayindex : online.Count));
                    }

                    psend();
                }
            }
            #endregion

            if (serial == -1 || serial == 0)
                send(conf.Redheadsound, rch_access: "apk");

            send(conf.HDVB, rch_access: "apk");
            send(conf.Kinotochka, rch_access: "apk,cors");

            if ((serial == -1 || (serial == 1 && !isanime)) && kinopoisk_id > 0)
                send(conf.CDNmovies, rch_access: "apk,cors");

            if (serial == -1 || serial == 0)
                send(conf.IframeVideo);

            if (kinopoisk_id > 0 && !isanime)
                send(conf.CDNvideohub, "cdnvideohub", "VideoHUB", rch_access: "apk,cors");

            #region ENG
            if ((original_language == null || original_language == "en") && conf.disableEng == false)
            {
                if (tmdb_id > 0 || (source != null && (source is "tmdb" or "cub")))
                {
                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Hydraflix.overridehost) || conf.Hydraflix.overridehosts?.Length > 0)
                        send(conf.Hydraflix, "hydraflix", "HydraFlix (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Vidsrc.overridehost) || conf.Vidsrc.overridehosts?.Length > 0)
                        send(conf.Vidsrc, "vidsrc", "VidSrc (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.VidLink.overridehost) || conf.VidLink.overridehosts?.Length > 0)
                        send(conf.VidLink, "vidlink", "VidLink (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Videasy.overridehost) || conf.Videasy.overridehosts?.Length > 0)
                        send(conf.Videasy, "videasy", "Videasy (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.MovPI.overridehost) || conf.MovPI.overridehosts?.Length > 0)
                        send(conf.MovPI, "movpi", "MovPI (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Smashystream.overridehost) || conf.Smashystream.overridehosts?.Length > 0)
                        send(conf.Smashystream, "smashystream", "SmashyStream (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Autoembed.overridehost) || conf.Autoembed.overridehosts?.Length > 0)
                        send(conf.Autoembed, "autoembed", "AutoEmbed (ENG)");

                    if (PlaywrightBrowser.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Playembed.overridehost) || conf.Playembed.overridehosts?.Length > 0)
                        send(conf.Playembed, "playembed", "PlayEmbed (ENG)");

                    if (Firefox.Status != PlaywrightStatus.disabled || !string.IsNullOrEmpty(conf.Twoembed.overridehost) || conf.Twoembed.overridehosts?.Length > 0)
                        send(conf.Twoembed, "twoembed", "2Embed (ENG)");

                    send(conf.Rgshows, "rgshows", "RgShows (ENG)");
                }
            }
            #endregion

            if (!life && conf.litejac)
                online.Add((null, "Торренты", "{localhost}/lite/jac", "jac", 200));

            #region checkOnlineSearch
            bool chos = conf.online.checkOnlineSearch && !string.IsNullOrEmpty(id);

            if (chos && IO.File.Exists("isdocker"))
            {
                string version = await Http.Get($"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}/version", timeoutSeconds: 4, headers: HeadersModel.Init("localrequest", AppInit.rootPasswd));
                if (version == null || !version.StartsWith(appversion))
                    chos = false;
            }

            if (chos)
            {
                string memkey = CrypTo.md5($"checkOnlineSearch:{id}:{serial}:{source?.Replace("tmdb", "")?.Replace("cub", "")}:{online.Count}:{(IsKitConf ? requestInfo.user_uid : null)}");

                if (!memoryCache.TryGetValue(memkey, out List<(string code, int index, bool work)> links) || !conf.multiaccess)
                {
                    var tasks = new List<Task>();
                    links = new List<(string code, int index, bool work)>(online.Count);
                    for (int i = 0; i < online.Count; i++)
                        links.Add(default);

                    memoryCache.Set(memkey, links, DateTime.Now.AddMinutes(5));

                    foreach (var o in online)
                    {
                        var tk = checkSearch(memkey, links, tasks.Count, o.init, o.index, o.name, o.url, o.plugin, id, imdb_id, kinopoisk_id, tmdb_id, title, original_title, original_language, source, year, serial, life, rchtype);
                        tasks.Add(tk);
                    }

                    if (life)
                        return Json(new { life = true, memkey, title = (fix_title ? title : null) });

                    await Task.WhenAll(tasks);
                }

                if (life)
                    return Json(new { life = true, memkey });

                return ContentTo($"[{string.Join(",", links.Where(i => i.code != null).OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code)).Replace("{localhost}", host)}]");
            }
            #endregion

            string online_result = string.Join(",", online.OrderBy(i => i.index).Select(i => "{\"name\":\"" + i.name + "\",\"url\":\"" + i.url + "\",\"balanser\":\"" + i.plugin + "\"}"));
            return ContentTo($"[{online_result.Replace("{localhost}", host)}]");
        }
        #endregion

        #region checkSearch
        async Task checkSearch(string memkey, List<(string code, int index, bool work)> links, int indexList, dynamic init, int index, string name, string uri, string plugin,
                               string id, string imdb_id, long kinopoisk_id, long tmdb_id, string title, string original_title, string original_language, string source,  int year, int serial, bool life, string rchtype)
        {
            try
            {
                string srq = uri.Replace("{localhost}", $"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}");
                var header = uri.Contains("{localhost}") ? HeadersModel.Init(("xhost", host), ("xscheme", HttpContext.Request.Scheme), ("localrequest", AppInit.rootPasswd)) : null;

                string checkuri = $"{srq}{(srq.Contains("?") ? "&" : "?")}id={HttpUtility.UrlEncode(id)}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&tmdb_id={tmdb_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&original_language={original_language}&source={source}&year={year}&serial={serial}&rchtype={rchtype}&checksearch=true";
                string res = await Http.Get(AccsDbInvk.Args(checkuri, HttpContext), timeoutSeconds: 10, headers: header);

                if (string.IsNullOrEmpty(res))
                    res = string.Empty;

                bool rch = res.Contains("\"rch\":true");
                bool work = rch || res.Contains("data-json=") 
                    || res.Contains("\"type\":\"movie\"") 
                    || res.Contains("\"type\":\"episode\"")
                    || res.Contains("\"type\":\"season\"");

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
                            case "vokino-alloha":
                            case "vokino-filmix":
                            case "alloha":
                            case "remux":
                            case "pidtor":
                            case "rhsprem":
                            case "animelib":
                            case "mirage":
                            case "videodb":
                            case "iptvonline":
                            case "plvideo":
                            case "rutubemovie":
                            case "vkmovie":
                                quality = " ~ 2160p";
                                break;
                            case "kinobase":
                            case "kinogo":
                            case "getstv":
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
                            case "aniliberty":
                            case "redheadsound":
                            case "iframevideo":
                            case "animego":
                            case "lostfilmhd":
                            case "vdbmovies":
                            case "collaps-dash":
                            case "fancdn":
                            case "cdnvideohub":
                            case "moonanime":
                            case "playembed":
                            case "rgshows":
                            case "twoembed":
                            case "vidsrc":
                            case "smashystream":
                            case "hydraflix":
                            case "movpi":
                            case "videasy":
                            case "vidlink":
                            case "autoembed":
                            case "veoveo":
                            case "vokino-vibix":
                            case "vokino-monframe":
                            case "vokino-remux":
                            case "vokino-ashdi":
                            case "vokino-hdvb":
                                quality = " ~ 1080p";
                                break;
                            case "voidboost":
                            case "animedia":
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

                links[indexList] = ("{" + $"\"name\":\"{name}\",\"url\":\"{uri}\",\"index\":{index},\"show\":{work.ToString().ToLower()},\"balanser\":\"{plugin}\",\"rch\":{rch.ToString().ToLower()}" + "}", index, work);
            }
            catch (Exception ex) { Console.WriteLine("checkSearch: " + ex.ToString()); }
        }
        #endregion
    }
}