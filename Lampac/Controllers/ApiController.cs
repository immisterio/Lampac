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

                    memoryCache.Set($"LampaWeb.index:{AppInit.conf.LampaWeb.index}", html, DateTime.Now.AddMinutes(5));
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

        #region Version / Headers / myip / testaccsdb / personal.lampa
        [Route("/version")]
        public ActionResult Version() => Content($"{appversion}.{minorversion}", contentType: "text/plain; charset=utf-8");

        [Route("/headers")]
        public ActionResult Headers() => Json(HttpContext.Request.Headers);

        [Route("/myip")]
        public ActionResult MyIP() => Content(HttpContext.Connection.RemoteIpAddress.ToString());

        [Route("/testaccsdb")]
        public ActionResult TestAccsdb() => Content("{\"accsdb\": false}");

        [Route("/personal.lampa")]
        [Route("/lampa-main/personal.lampa")]
        [Route("/{myfolder}/personal.lampa")]
        public ActionResult PersonalLampa(string myfolder) => StatusCode(200);
        #endregion


        #region app.min.js
        [Route("lampa-{type}/app.min.js")]
        public ActionResult LampaApp(string type)
        {
            if (!memoryCache.TryGetValue($"ApiController:{type}:{host}:app.min.js", out string file))
            {
                file = IO.File.ReadAllText($"wwwroot/lampa-{type}/app.min.js");

                file = file.Replace("http://lite.lampa.mx", $"{host}/lampa-{type}");
                file = file.Replace("https://yumata.github.io/lampa-lite", $"{host}/lampa-{type}");

                file = file.Replace("http://lampa.mx", $"{host}/lampa-{type}");
                file = file.Replace("https://yumata.github.io/lampa", $"{host}/lampa-{type}");

                file = file.Replace("window.lampa_settings.dcma = dcma;", "window.lampa_settings.fixdcma = true;");
                file = file.Replace("Storage.get('vpn_checked_ready', 'false')", "true");

                memoryCache.Set($"ApiController:{type}:app.min.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region samsung.wgt
        [HttpGet]
        [Route("samsung.wgt")]
        public ActionResult SamsWgt(string overwritehost)
        {
            if (!IO.File.Exists("widgets/samsung/loader.js"))
                return Content(string.Empty);

            string wgt = $"widgets/{CrypTo.md5(overwritehost ?? host + "v2")}.wgt";
            if (IO.File.Exists(wgt))
                return File(IO.File.OpenRead(wgt), "application/octet-stream");

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

            string loaderhashsha512 = gethash("widgets/samsung/publish/loader.js");
            string apphashsha512 = gethash("widgets/samsung/publish/app.js");
            string confighashsha512 = gethash("widgets/samsung/publish/config.xml");
            string iconhashsha512 = gethash("widgets/samsung/publish/icon.png");
            string logohashsha512 = gethash("widgets/samsung/publish/logo_appname_fg.png");

            string author_sigxml = IO.File.ReadAllText("widgets/samsung/author-signature.xml");
            author_sigxml = author_sigxml.Replace("loaderhashsha512", loaderhashsha512).Replace("apphashsha512", apphashsha512)
                                         .Replace("iconhashsha512", iconhashsha512).Replace("logohashsha512", logohashsha512)
                                         .Replace("confighashsha512", confighashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/author-signature.xml", author_sigxml);

            string authorsignaturehashsha512 = gethash("widgets/samsung/publish/author-signature.xml");
            string sigxml1 = IO.File.ReadAllText("widgets/samsung/signature1.xml");
            sigxml1 = sigxml1.Replace("loaderhashsha512", loaderhashsha512).Replace("apphashsha512", apphashsha512)
                             .Replace("confighashsha512", confighashsha512).Replace("authorsignaturehashsha512", authorsignaturehashsha512)
                             .Replace("iconhashsha512", iconhashsha512).Replace("logohashsha512", logohashsha512);
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
            return Content(FileCache.ReadAllText("msx.json").Replace("{localhost}", host), contentType: "application/json; charset=utf-8");
        }
        #endregion

        #region tmdbproxy.js
        [HttpGet]
        [Route("tmdbproxy.js")]
        public ActionResult TmdbProxy()
        {
            return Content(FileCache.ReadAllText("plugins/tmdbproxy.js").Replace("{localhost}", host), contentType: "application/javascript; charset=utf-8");
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
                }
                else
                {
                    if (AppInit.conf.LampaWeb.initPlugins.dlna && AppInit.modules.FirstOrDefault(i => i.dll == "DLNA.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/dlna.js\",\"status\": 1,\"name\": \"DLNA\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.tracks && AppInit.modules.FirstOrDefault(i => i.dll == "Tracks.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/tracks.js\",\"status\": 1,\"name\": \"Tracks.js\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.tmdbProxy)
                        initiale += "{\"url\": \"{localhost}/tmdbproxy.js\",\"status\": 1,\"name\": \"TMDB Proxy\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.online && AppInit.modules.FirstOrDefault(i => i.dll == "Online.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/online.js\",\"status\": 1,\"name\": \"Онлайн\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.sisi && AppInit.modules.FirstOrDefault(i => i.dll == "SISI.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/sisi.js\",\"status\": 1,\"name\": \"Клубничка\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.timecode)
                        initiale += "{\"url\": \"{localhost}/timecode.js\",\"status\": 1,\"name\": \"Синхронизация тайм-кодов\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.torrserver && AppInit.modules.FirstOrDefault(i => i.dll == "TorrServer.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/ts.js\",\"status\": 1,\"name\": \"TorrServer\",\"author\": \"lampac\"},";

                    if (AppInit.conf.accsdb.enable)
                        file = file.Replace("{deny}", FileCache.ReadAllText("plugins/deny.js").Replace("{cubMesage}", AppInit.conf.accsdb.authMesage));

                    if (AppInit.conf.pirate_store) 
                        file = file.Replace("{pirate_store}", FileCache.ReadAllText("plugins/pirate_store.js"));
                }
            }

            file = file.Replace("{initiale}", Regex.Replace(initiale, ",$", ""));
            file = file.Replace("{localhost}", host);
            file = file.Replace("{deny}", string.Empty);
            file = file.Replace("{pirate_store}", string.Empty);

            if (AppInit.modules != null && AppInit.modules.FirstOrDefault(i => i.dll == "JacRed.dll" && i.enable) != null)
                file = file.Replace("{jachost}", Regex.Replace(host, "^https?://", ""));
            else
                file = file.Replace("{jachost}", "redapi.cfhttp.top");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion
    }
}