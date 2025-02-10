using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Text.RegularExpressions;
using System;
using IO = System.IO;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;
using System.Web;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http;
using Shared.Engine.CORE;

namespace Lampac.Controllers
{
    public class ApiController : BaseController
    {
        #region Index
        [Route("/")]
        public ActionResult Index()
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.LampaWeb.index) || !IO.File.Exists($"wwwroot/{AppInit.conf.LampaWeb.index}"))
                return Content("api work", contentType: "text/plain; charset=utf-8");

            if (AppInit.conf.LampaWeb.basetag && Regex.IsMatch(AppInit.conf.LampaWeb.index, "/[^\\./]+\\.html$"))
            {
                if (!memoryCache.TryGetValue($"LampaWeb.index:{AppInit.conf.LampaWeb.index}", out string html))
                {
                    html = IO.File.ReadAllText($"wwwroot/{AppInit.conf.LampaWeb.index}");
                    html = html.Replace("<head>", $"<head><base href=\"/{Regex.Match(AppInit.conf.LampaWeb.index, "^([^/]+)/").Groups[1].Value}/\" />");

                    memoryCache.Set($"LampaWeb.index:{AppInit.conf.LampaWeb.index}", html, DateTime.Now.AddMinutes(1));
                }

                return Content(html, contentType: "text/html; charset=utf-8");
            }

            return LocalRedirect($"/{AppInit.conf.LampaWeb.index}");
        }
        #endregion

        #region Extensions
        [Route("/extensions")]
        public ActionResult Extensions()
        {
            if (!memoryCache.TryGetValue("LampaWeb.extensions", out string json))
            {
                json = IO.File.ReadAllText("plugins/extensions.json");
                json = json.Replace("\n", "").Replace("\r", "");

                memoryCache.Set("LampaWeb.extensions", json, DateTime.Now.AddMinutes(5));
            }

            return Content(json.Replace("{localhost}", host), contentType: "application/json; charset=utf-8");
        }
        #endregion

        #region Version / Headers / geo / myip / testaccsdb / reqinfo / personal.lampa
        [Route("/version")]
        public ActionResult Version() => Content($"{appversion}.{minorversion}", contentType: "text/plain; charset=utf-8");

        [Route("/headers")]
        public ActionResult Headers() => Json(HttpContext.Request.Headers);

        [Route("/geo")]
        public ActionResult Geo(string select, string ip)
        {
            if (select == "ip")
                return Content(ip ?? requestInfo.IP);

            string country = requestInfo.Country;
            if (ip != null)
                country = GeoIP2.Country(ip);

            if (select == "country")
                return Content(country);

            return Json(new
            { 
                ip = ip ?? requestInfo.IP,
                country
            });
        }

        [Route("/myip")]
        public ActionResult MyIP() => Content(requestInfo.IP);

        [Route("/reqinfo")]
        public ActionResult Reqinfo() => ContentTo(JsonConvert.SerializeObject(requestInfo, new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        }));

        [Route("/testaccsdb")]
        public ActionResult TestAccsdb() => Content("{\"accsdb\": false}", "application/json; charset=utf-8");

        [Route("/personal.lampa")]
        [Route("/lampa-main/personal.lampa")]
        [Route("/{myfolder}/personal.lampa")]
        public ActionResult PersonalLampa(string myfolder) => StatusCode(200);
        #endregion

        #region Sync
        [Route("/api/sync")]
        public ActionResult Sync()
        {
            var sync = AppInit.conf.sync;
            if (!requestInfo.IsLocalRequest || !sync.enable || sync.type != "master")
                return Content("error");

            if (sync.initconf == "current")
                return Content(JsonConvert.SerializeObject(AppInit.conf), "application/json; charset=utf-8");

            var init = new AppInit();

            string confile = "sync.conf";
            if (sync.override_conf != null && sync.override_conf.TryGetValue(requestInfo.IP, out string _conf))
                confile = _conf;

            if (IO.File.Exists(confile))
                init = JsonConvert.DeserializeObject<AppInit>(IO.File.ReadAllText(confile));

            init.accsdb.users = AppInit.conf.accsdb.users;

            string json = JsonConvert.SerializeObject(init);
            json = json.Replace("{server_ip}", requestInfo.IP);

            return Content(json, "application/json; charset=utf-8");
        }
        #endregion


        #region app.min.js
        [Route("/app.min.js")]
        [Route("{type}/app.min.js")]
        public ActionResult LampaApp(string type)
        {
            if (string.IsNullOrEmpty(type))
            {
                if (AppInit.conf.LampaWeb.path != null)
                {
                    type = AppInit.conf.LampaWeb.path;
                }
                else
                {
                    if (AppInit.conf.LampaWeb.index == null || !AppInit.conf.LampaWeb.index.Contains("/"))
                        return Content(string.Empty, "application/javascript; charset=utf-8");

                    type = AppInit.conf.LampaWeb.index.Split("/")[0];
                }
            }

            if (!memoryCache.TryGetValue($"ApiController:{type}:{host}:app.min.js", out string file))
            {
                file = IO.File.ReadAllText($"wwwroot/{type}/app.min.js");

                file = file.Replace("http://lite.lampa.mx", $"{host}/{type}");
                file = file.Replace("https://yumata.github.io/lampa-lite", $"{host}/{type}");

                file = file.Replace("http://lampa.mx", $"{host}/{type}");
                file = file.Replace("https://yumata.github.io/lampa", $"{host}/{type}");

                file = file.Replace("window.lampa_settings.dcma = dcma;", "window.lampa_settings.fixdcma = true;");
                file = file.Replace("Storage.get('vpn_checked_ready', 'false')", "true");

                file = file.Replace("status$1 = false;", "status$1 = true;"); // local apk to personal.lampa 

                memoryCache.Set($"ApiController:{type}:app.min.js", file, DateTime.Now.AddMinutes(5));
            }

            if (AppInit.conf.cub.enable)
            {
                file = file.Replace("protocol + mirror + '/api/checker'", $"'{host}/cub/api/checker'");
                file = file.Replace("Utils$2.protocol() + object$2.cub_domain", $"'{host}/cub/{AppInit.conf.cub.domain}'");
            }

            if (AppInit.conf.LampaWeb.appReplace != null)
            {
                foreach (var r in AppInit.conf.LampaWeb.appReplace)
                {
                    string val = r.Value.Replace("{localhost}", host).Replace("{host}", Regex.Replace(host, "^https?://", ""));
                    file = Regex.Replace(file, r.Key, val, RegexOptions.IgnoreCase);
                }
            }

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion

        #region app.css
        [Route("/css/app.css")]
        public ActionResult LampaAppCss()
        {
            string path;
            if (AppInit.conf.LampaWeb.path != null)
            {
                path = AppInit.conf.LampaWeb.path;
            }
            else
            {
                if (AppInit.conf.LampaWeb.index == null || !AppInit.conf.LampaWeb.index.Contains("/"))
                    return Content(string.Empty, "text/css; charset=utf-8");

                path = AppInit.conf.LampaWeb.index.Split("/")[0];
            }

            string css = FileCache.ReadAllText($"wwwroot/{path}/css/app.css");

            return Content(css, "text/css; charset=utf-8");
        }
        #endregion


        #region samsung.wgt
        [HttpGet]
        [Route("samsung.wgt")]
        public ActionResult SamsWgt(string overwritehost)
        {
            if (!IO.File.Exists("widgets/samsung/loader.js"))
                return Content(string.Empty);

            string wgt = $"widgets/{CrypTo.md5(overwritehost ?? host + "v3")}.wgt";
            if (IO.File.Exists(wgt))
                return File(IO.File.OpenRead(wgt), "application/octet-stream");

            string index = IO.File.ReadAllText("widgets/samsung/index.html");
            IO.File.WriteAllText("widgets/samsung/publish/index.html", index.Replace("{localhost}", overwritehost ?? host));

            string loader = IO.File.ReadAllText("widgets/samsung/loader.js");
            IO.File.WriteAllText("widgets/samsung/publish/loader.js", loader.Replace("{localhost}", overwritehost ?? host));

            string app = IO.File.ReadAllText("widgets/samsung/app.js");
            IO.File.WriteAllText("widgets/samsung/publish/app.js", app.Replace("{localhost}", overwritehost ?? host));

            IO.File.Copy("widgets/samsung/icon.png", "widgets/samsung/publish/icon.png", overwrite: true);
            IO.File.Copy("widgets/samsung/logo_appname_fg.png", "widgets/samsung/publish/logo_appname_fg.png", overwrite: true);
            IO.File.Copy("widgets/samsung/config.xml", "widgets/samsung/publish/config.xml", overwrite: true);

            string gethash(string file)
            {
                using (SHA512 sha = SHA512.Create())
                {
                    return Convert.ToBase64String(sha.ComputeHash(IO.File.ReadAllBytes(file)));
                    //digestValue = hash.Remove(76) + "\n" + hash.Remove(0, 76);
                }
            }

            string indexhashsha512 = gethash("widgets/samsung/publish/index.html");
            string loaderhashsha512 = gethash("widgets/samsung/publish/loader.js");
            string apphashsha512 = gethash("widgets/samsung/publish/app.js");
            string confighashsha512 = gethash("widgets/samsung/publish/config.xml");
            string iconhashsha512 = gethash("widgets/samsung/publish/icon.png");
            string logohashsha512 = gethash("widgets/samsung/publish/logo_appname_fg.png");

            string author_sigxml = IO.File.ReadAllText("widgets/samsung/author-signature.xml");
            author_sigxml = author_sigxml.Replace("loaderhashsha512", loaderhashsha512).Replace("apphashsha512", apphashsha512)
                                         .Replace("iconhashsha512", iconhashsha512).Replace("logohashsha512", logohashsha512)
                                         .Replace("confighashsha512", confighashsha512)
                                         .Replace("indexhashsha512", indexhashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/author-signature.xml", author_sigxml);

            string authorsignaturehashsha512 = gethash("widgets/samsung/publish/author-signature.xml");
            string sigxml1 = IO.File.ReadAllText("widgets/samsung/signature1.xml");
            sigxml1 = sigxml1.Replace("loaderhashsha512", loaderhashsha512).Replace("apphashsha512", apphashsha512)
                             .Replace("confighashsha512", confighashsha512).Replace("authorsignaturehashsha512", authorsignaturehashsha512)
                             .Replace("iconhashsha512", iconhashsha512).Replace("logohashsha512", logohashsha512).Replace("indexhashsha512", indexhashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/signature1.xml", sigxml1);

            ZipFile.CreateFromDirectory("widgets/samsung/publish/", wgt);

            return File(IO.File.OpenRead(wgt), "application/octet-stream");
        }
        #endregion

        #region MSX
        [HttpGet]
        [Route("msx/start.json")]
        public ActionResult MSX()
        {
            return Content(FileCache.ReadAllText("msx.json").Replace("{localhost}", host), "application/json; charset=utf-8");
        }
        #endregion

        #region startpage.js
        [HttpGet]
        [Route("startpage.js")]
        public ActionResult StartPage()
        {
            return Content(FileCache.ReadAllText("plugins/startpage.js").Replace("{localhost}", host), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region lampainit.js
        [HttpGet]
        [Route("lampainit.js")]
        public ActionResult LamInit(bool lite)
        {
            string initiale = string.Empty;
            string file = FileCache.ReadAllText($"plugins/{(lite ? "liteinit" : "lampainit")}.js");

            if (AppInit.modules != null)
            {
                if (lite)
                {
                    if (AppInit.conf.LampaWeb.initPlugins.online && AppInit.modules.FirstOrDefault(i => i.dll == "Online.dll" && i.enable) != null)
                        initiale += "\"{localhost}/lite.js\",";

                    if (AppInit.conf.LampaWeb.initPlugins.sisi && AppInit.modules.FirstOrDefault(i => i.dll == "SISI.dll" && i.enable) != null)
                        initiale += "\"{localhost}/sisi.js?lite=true\",";

                    if (AppInit.conf.LampaWeb.initPlugins.sync)
                        initiale += "\"{localhost}/sync.js?lite=true\",";
                }
                else
                {
                    if (AppInit.conf.LampaWeb.initPlugins.dlna && AppInit.modules.FirstOrDefault(i => i.dll == "DLNA.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/dlna.js\",\"status\": 1,\"name\": \"DLNA\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.tracks && AppInit.modules.FirstOrDefault(i => i.dll == "Tracks.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/tracks.js\",\"status\": 1,\"name\": \"Tracks.js\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.tmdbProxy)
                        initiale += "{\"url\": \"{localhost}/tmdbproxy.js\",\"status\": 1,\"name\": \"TMDB Proxy\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.cubProxy)
                        initiale += "{\"url\": \"{localhost}/cubproxy.js\",\"status\": 1,\"name\": \"CUB Proxy\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.online && AppInit.modules.FirstOrDefault(i => i.dll == "Online.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/online.js\",\"status\": 1,\"name\": \"Онлайн\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.sisi && AppInit.modules.FirstOrDefault(i => i.dll == "SISI.dll" && i.enable) != null)
                    {
                        initiale += "{\"url\": \"{localhost}/sisi.js\",\"status\": 1,\"name\": \"Клубничка\",\"author\": \"lampac\"},";
                        initiale += "{\"url\": \"{localhost}/startpage.js\",\"status\": 1,\"name\": \"Стартовая страница\",\"author\": \"lampac\"},";
                    }

                    if (AppInit.conf.LampaWeb.initPlugins.timecode)
                        initiale += "{\"url\": \"{localhost}/timecode.js\",\"status\": 1,\"name\": \"Синхронизация тайм-кодов\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.sync)
                        initiale += "{\"url\": \"{localhost}/sync.js\",\"status\": 1,\"name\": \"Синхронизация\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.torrserver && AppInit.modules.FirstOrDefault(i => i.dll == "TorrServer.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/ts.js\",\"status\": 1,\"name\": \"TorrServer\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.backup)
                        initiale += "{\"url\": \"{localhost}/backup.js\",\"status\": 1,\"name\": \"Backup\",\"author\": \"lampac\"},";

                    if (AppInit.conf.pirate_store) 
                        file = file.Replace("{pirate_store}", FileCache.ReadAllText("plugins/pirate_store.js"));

                    if (AppInit.conf.accsdb.enable)
                        file = file.Replace("{deny}", FileCache.ReadAllText("plugins/deny.js").Replace("{cubMesage}", AppInit.conf.accsdb.authMesage));
                }
            }

            if (IO.File.Exists("plugins/lampainit-invc.my.js"))
                file = file.Replace("{lampainit-invc}", FileCache.ReadAllText("plugins/lampainit-invc.my.js"));
            file = file.Replace("{lampainit-invc}", string.Empty);

            file = file.Replace("{initiale}", Regex.Replace(initiale, ",$", ""));
            file = file.Replace("{country}", requestInfo.Country);
            file = file.Replace("{localhost}", host);
            file = file.Replace("{deny}", string.Empty);
            file = file.Replace("{pirate_store}", string.Empty);

            if (AppInit.modules != null && AppInit.modules.FirstOrDefault(i => i.dll == "JacRed.dll" && i.enable) != null)
                file = file.Replace("{jachost}", Regex.Replace(host, "^https?://", ""));
            else
                file = file.Replace("{jachost}", "redapi.cfhttp.top");

            #region full_btn_priority_hash
            string online_version = Regex.Match(FileCache.ReadAllText("plugins/online.js"), "version: '([^']+)'").Groups[1].Value;

            string LampaUtilshash(string input)
            {
                if (!AppInit.conf.online.version)
                    input = input.Replace($"v{online_version}", "");

                string str = (input ?? string.Empty);
                int hash = 0;

                if (str.Length == 0) return hash.ToString();

                for (int i = 0; i < str.Length; i++)
                {
                    int _char = str[i];

                    hash = (hash << 5) - hash + _char;
                    hash = hash & hash; // Преобразование в 32-битное целое число
                }

                return Math.Abs(hash).ToString();
            }

            string full_btn_priority_hash = LampaUtilshash($"<div class=\"full-start__button selector view--online lampac--button\" data-subtitle=\"{AppInit.conf.online.name} v{online_version}\">\n        <svg xmlns=\"http://www.w3.org/2000/svg\" version=\"1.1\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 392.697 392.697\" xml:space=\"preserve\">\n            <path d=\"M21.837,83.419l36.496,16.678L227.72,19.886c1.229-0.592,2.002-1.846,1.98-3.209c-0.021-1.365-0.834-2.592-2.082-3.145\n                L197.766,0.3c-0.903-0.4-1.933-0.4-2.837,0L21.873,77.036c-1.259,0.559-2.073,1.803-2.081,3.18\n                C19.784,81.593,20.584,82.847,21.837,83.419z\" fill=\"currentColor\"></path>\n            <path d=\"M185.689,177.261l-64.988-30.01v91.617c0,0.856-0.44,1.655-1.167,2.114c-0.406,0.257-0.869,0.386-1.333,0.386\n                c-0.368,0-0.736-0.082-1.079-0.244l-68.874-32.625c-0.869-0.416-1.421-1.293-1.421-2.256v-92.229L6.804,95.5\n                c-1.083-0.496-2.344-0.406-3.347,0.238c-1.002,0.645-1.608,1.754-1.608,2.944v208.744c0,1.371,0.799,2.615,2.045,3.185\n                l178.886,81.768c0.464,0.211,0.96,0.315,1.455,0.315c0.661,0,1.318-0.188,1.892-0.555c1.002-0.645,1.608-1.754,1.608-2.945\n                V180.445C187.735,179.076,186.936,177.831,185.689,177.261z\" fill=\"currentColor\"></path>\n            <path d=\"M389.24,95.74c-1.002-0.644-2.264-0.732-3.347-0.238l-178.876,81.76c-1.246,0.57-2.045,1.814-2.045,3.185v208.751\n                c0,1.191,0.606,2.302,1.608,2.945c0.572,0.367,1.23,0.555,1.892,0.555c0.495,0,0.991-0.104,1.455-0.315l178.876-81.768\n                c1.246-0.568,2.045-1.813,2.045-3.185V98.685C390.849,97.494,390.242,96.384,389.24,95.74z\" fill=\"currentColor\"></path>\n            <path d=\"M372.915,80.216c-0.009-1.377-0.823-2.621-2.082-3.18l-60.182-26.681c-0.938-0.418-2.013-0.399-2.938,0.045\n                l-173.755,82.992l60.933,29.117c0.462,0.211,0.958,0.316,1.455,0.316s0.993-0.105,1.455-0.316l173.066-79.092\n                C372.122,82.847,372.923,81.593,372.915,80.216z\" fill=\"currentColor\"></path>\n        </svg>\n\n        <span>Онлайн</span>\n    </div>");

            file = file.Replace("{full_btn_priority_hash}", full_btn_priority_hash);
            file = file.Replace("{btn_priority_forced}", AppInit.conf.online.btn_priority_forced.ToString().ToLower());
            #endregion

            #region domain token
            if (!string.IsNullOrEmpty(AppInit.conf.accsdb.domainId_pattern))
            {
                string token = Regex.Match(HttpContext.Request.Host.Host, AppInit.conf.accsdb.domainId_pattern).Groups[1].Value;
                file = file.Replace("{token}", token);
            }
            else file = file.Replace("{token}", string.Empty);
            #endregion

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region on.js
        [HttpGet]
        [Route("on.js")]
        [Route("on/js/{token}")]
        [Route("on/h/{token}")]
        [Route("on/{token}")]
        public ActionResult LamOnInit(string token, bool adult = true)
        {
            if (adult && HttpContext.Request.Path.Value.StartsWith("/on/h/"))
                adult = false;

            List<string> plugins = new List<string>(7);
            string file = FileCache.ReadAllText("plugins/on.js");

            if (AppInit.modules != null)
            {
                void send(string name, bool worktoken)
                {
                    if (worktoken && !string.IsNullOrEmpty(token))
                    {
                        plugins.Add($"\"{{localhost}}/{name}/js/{HttpUtility.UrlEncode(token)}\"");
                    }
                    else
                    {
                        plugins.Add($"\"{{localhost}}/{name}.js\"");
                    }
                }

                if (AppInit.conf.LampaWeb.initPlugins.dlna && AppInit.modules.FirstOrDefault(i => i.dll == "DLNA.dll" && i.enable) != null)
                    send("dlna", true);

                if (AppInit.conf.LampaWeb.initPlugins.tracks && AppInit.modules.FirstOrDefault(i => i.dll == "Tracks.dll" && i.enable) != null)
                    send("tracks", true);

                if (AppInit.conf.LampaWeb.initPlugins.tmdbProxy)
                    send("tmdbproxy", true);

                if (AppInit.conf.LampaWeb.initPlugins.online && AppInit.modules.FirstOrDefault(i => i.dll == "Online.dll" && i.enable) != null)
                    send("online", true);

                if (adult)
                {
                    if (AppInit.conf.LampaWeb.initPlugins.sisi && AppInit.modules.FirstOrDefault(i => i.dll == "SISI.dll" && i.enable) != null)
                    {
                        send("sisi", true);
                        send("startpage", false);
                    }
                }

                if (AppInit.conf.LampaWeb.initPlugins.timecode)
                    send("timecode", true);

                if (AppInit.conf.LampaWeb.initPlugins.sync)
                    send("sync", true);

                if (AppInit.conf.LampaWeb.initPlugins.torrserver && AppInit.modules.FirstOrDefault(i => i.dll == "TorrServer.dll" && i.enable) != null)
                    send("ts", true);

                if (AppInit.conf.LampaWeb.initPlugins.backup)
                    send("backup", true);
            }

            if (plugins.Count == 0)
                file = file.Replace("{plugins}", string.Empty);
            else
            {
                file = file.Replace("{plugins}", string.Join(",", plugins));
            }

            file = file.Replace("{country}", requestInfo.Country);
            file = file.Replace("{localhost}", host);

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region privateinit.js
        [HttpGet]
        [Route("privateinit.js")]
        public ActionResult PrivateInit()
        {
            var user = requestInfo.user;
            if (user == null || user.ban || DateTime.UtcNow > user.expires)
                return Content(string.Empty, "application/javascript; charset=utf-8");

            string initiale = string.Empty;
            string file = FileCache.ReadAllText("plugins/privateinit.js");

            file = file.Replace("{country}", requestInfo.Country);
            file = file.Replace("{localhost}", host);

            if (AppInit.modules != null && AppInit.modules.FirstOrDefault(i => i.dll == "JacRed.dll" && i.enable) != null)
                file = file.Replace("{jachost}", Regex.Replace(host, "^https?://", ""));
            else
                file = file.Replace("{jachost}", "redapi.cfhttp.top");

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion


        #region backup.js
        [HttpGet]
        [Route("backup.js")]
        [Route("backup/js/{token}")]
        public ActionResult Backup(string token)
        {
            if (!AppInit.conf.storage.enable)
                return Content(string.Empty, "application/javascript; charset=utf-8");

            string file = FileCache.ReadAllText("plugins/backup.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion

        #region sync.js
        [HttpGet]
        [Route("sync.js")]
        [Route("sync/js/{token}")]
        public ActionResult SyncJS(string token, bool lite)
        {
            if (!AppInit.conf.storage.enable)
                return Content(string.Empty, "application/javascript; charset=utf-8");

            string file = FileCache.ReadAllText($"plugins/{(lite ? "sync_lite" : "sync")}.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion

        #region invc-ws.js
        [HttpGet]
        [Route("invc-ws.js")]
        [Route("invc-ws/js/{token}")]
        public ActionResult InvcSyncJS(string token)
        {
            string file = FileCache.ReadAllText("plugins/invc-ws.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, "application/javascript; charset=utf-8");
        }
        #endregion

        #region invc-rch.js
        [HttpGet]
        [Route("invc-rch.js")]
        public ActionResult InvcRchJS()
        {
            return Content(FileCache.ReadAllText("plugins/invc-rch.js").Replace("{localhost}", host), "application/javascript; charset=utf-8");
        }
        #endregion


        #region weblog
        [HttpGet]
        [Route("weblog")]
        public ActionResult WebLog(string token, string pattern)
        {
            if (!AppInit.conf.weblog.enable)
                return Content("Включите weblog в init.conf\n\n\"weblog\": {\n   \"enable\": true\n}", contentType: "text/plain; charset=utf-8");

            if (!string.IsNullOrEmpty(AppInit.conf.weblog.token) && token != AppInit.conf.weblog.token)
                return Content("Используйте /weblog?token=my_key\n\n\"weblog\": {\n   \"enable\": true,\n   \"token\": \"my_key\"\n}", contentType: "text/plain; charset=utf-8");

            string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>weblog</title>
</head>
<body style='margin: 0px;'>
    <div id='log'></div>
    <script src='https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/6.0.25/signalr.min.js'></script>
    <script>
        const hubConnection = new signalR.HubConnectionBuilder()
            .withUrl('/ws')
            .build();
 
		function send(message)
		{" +
        (string.IsNullOrEmpty(pattern) ? "" : "if (message.indexOf('"+pattern+ "') === -1) return;")
        +@"

			var par = document.getElementById('log');
			
			let messageElement = document.createElement('hr');
			messageElement.style.cssText = ' margin-bottom: 2.5em; margin-top: 2.5em;';
			par.insertBefore(messageElement, par.children[0]);
 
            messageElement = document.createElement('pre');
			messageElement.style.cssText = 'padding: 10px; background: cornsilk; white-space: pre-wrap; word-wrap: break-word;';
            messageElement.textContent = message;
			par.insertBefore(messageElement, par.children[0]);
		}
 
		hubConnection.on('Receive', function(message, plugin) {
            send(message);
		});

		hubConnection.onclose(function(err) {
            send(err.toString());
		});
 
        hubConnection.start()
            .then(function () {
				hubConnection.invoke('RegistryWebLog', '" + token+@"');
            })
            .catch(function (err) {
                send(err.toString());
            });
    </script>
</body>
</html>";

            return Content(html, "text/html; charset=utf-8");
        }
        #endregion
    }
}