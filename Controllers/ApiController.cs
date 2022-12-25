using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Web;
using Microsoft.Extensions.Caching.Memory;
using System;
using IO = System.IO;
using System.Security.Cryptography;
using System.IO.Compression;

namespace Lampac.Controllers
{
    public class ApiController : BaseController
    {
        [Route("/")]
        public ActionResult Index()
        {
            if (!string.IsNullOrWhiteSpace(AppInit.conf.LampaWeb.index) && IO.File.Exists($"wwwroot/{AppInit.conf.LampaWeb.index}"))
                return LocalRedirect($"/{AppInit.conf.LampaWeb.index}");

            return Content("api work", contentType: "text/plain; charset=utf-8");
        }

        #region samsung.wgt
        [HttpGet]
        [Route("samsung.wgt")]
        public ActionResult SamsWgt()
        {
            if (!IO.File.Exists("widgets/samsung/loader.js"))
                return Content(string.Empty);

            string wgt = $"widgets/{CrypTo.md5(AppInit.Host(HttpContext))}.wgt";
            if (IO.File.Exists(wgt))
                return File(IO.File.OpenRead(wgt), "application/octet-stream");

            string loader = IO.File.ReadAllText("widgets/samsung/loader.js");
            IO.File.WriteAllText("widgets/samsung/publish/loader.js", loader.Replace("{localhost}", AppInit.Host(HttpContext)));

            IO.File.Copy("widgets/samsung/icon.png", "widgets/samsung/publish/icon.png", overwrite: true);
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
            string iconhashsha512 = gethash("widgets/samsung/publish/icon.png");

            string sigxml1 = IO.File.ReadAllText("widgets/samsung/signature1.xml");
            sigxml1 = sigxml1.Replace("loaderhashsha512", loaderhashsha512).Replace("iconhashsha512", iconhashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/signature1.xml", sigxml1);

            string author_sigxml = IO.File.ReadAllText("widgets/samsung/author-signature.xml");
            author_sigxml = author_sigxml.Replace("loaderhashsha512", loaderhashsha512).Replace("iconhashsha512", iconhashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/author-signature.xml", author_sigxml);

            ZipFile.CreateFromDirectory("widgets/samsung/publish/", wgt);

            return File(IO.File.OpenRead(wgt), "application/octet-stream");
        }
        #endregion

        #region MSX
        [HttpGet]
        [Route("msx/start.json")]
        public ActionResult MSX()
        {
            if (!memoryCache.TryGetValue("ApiController:msx.json", out string file))
            {
                file = IO.File.ReadAllText("msx.json");
                memoryCache.Set("ApiController:msx.json", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", AppInit.Host(HttpContext));
            return Content(file, contentType: "application/json; charset=utf-8");
        }
        #endregion

        #region lampainit.js
        [HttpGet]
        [Route("lampainit.js")]
        public ActionResult LamInit(bool lite)
        {
            if (!memoryCache.TryGetValue($"ApiController:lampainit.js:{lite}", out string file))
            {
                file = IO.File.ReadAllText($"plugins/{(lite ? "liteinit" : "lampainit")}.js");
                memoryCache.Set($"ApiController:lampainit.js:{lite}", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", AppInit.Host(HttpContext));
            file = file.Replace("{jachost}", AppInit.Host(HttpContext).Replace("https://", "").Replace("http://", ""));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region sisi.js
        [HttpGet]
        [Route("sisi.js")]
        public ActionResult Sisi()
        {
            if (!memoryCache.TryGetValue("ApiController:sisi.js", out string file))
            {
                file = IO.File.ReadAllText("plugins/sisi.js");
                memoryCache.Set("ApiController:sisi.js", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/sisi");
            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region online.js
        [HttpGet]
        [Route("online.js")]
        public ActionResult Online()
        {
            if (!memoryCache.TryGetValue("ApiController:online.js", out string file))
            {
                file = IO.File.ReadAllText("plugins/online.js");
                memoryCache.Set("ApiController:online.js", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("http://127.0.0.1:9118", AppInit.Host(HttpContext));
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region lite.js
        [HttpGet]
        [Route("lite.js")]
        async public Task<ActionResult> Lite()
        {
            if (!memoryCache.TryGetValue("ApiController:lite.js", out string file))
            {
                file = await IO.File.ReadAllTextAsync("plugins/lite.js");
                memoryCache.Set("ApiController:lite.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite"), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region events
        [HttpGet]
        [Route("lifeevents")]
        public ActionResult LifeEvents(int id)
        {
            string json = null;

            if (memoryCache.TryGetValue($"ApiController:checkOnlineSearch:{id}", out (bool ready, int tasks, string online) res))
            {
                string online = res.online?.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite") ?? string.Empty;
                json = "{"+ $"\"ready\":{res.ready.ToString().ToLower()},\"tasks\":{res.tasks},\"online\":[{Regex.Replace(online, ",$", "")}]" + "}";
            }

            return Content(json ?? "{\"ready\":false,\"tasks\":0,\"online\":[]}", contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("lite/events")]
        async public Task<ActionResult> Events(int id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, int serial = -1, bool life = false)
        {
            string online = string.Empty;
            bool isanime = original_language == "ja";

            if (!life && AppInit.conf.jac.litejac)
                online += "{\"name\":\"Jackett\",\"url\":\"{localhost}/jac\"},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.KinoPub.token))
                online += "{\"name\":\"KinoPub\",\"url\":\"{localhost}/kinopub\"},";

            if (AppInit.conf.Filmix.enable)
                online += "{\"name\":\"Filmix\",\"url\":\"{localhost}/filmix\"},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Alloha.token))
                online += "{\"name\":\"Alloha\",\"url\":\"{localhost}/alloha\"},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Bazon.token))
                online += "{\"name\":\"Bazon\",\"url\":\"{localhost}/bazon\"},";

            if (AppInit.conf.VideoDB.enable)
                online += "{\"name\":\"VideoDB\",\"url\":\"{localhost}/videodb\"},";

            if (AppInit.conf.Zetflix.enable)
                online += "{\"name\":\"Zetflix\",\"url\":\"{localhost}/zetflix\"},";

            if (AppInit.conf.Kinobase.enable)
                online += "{\"name\":\"Kinobase\",\"url\":\"{localhost}/kinobase\"},";

            if (AppInit.conf.Rezka.enable)
                online += "{\"name\":\"HDRezka\",\"url\":\"{localhost}/rezka\"},";

            if (AppInit.conf.VCDN.enable)
                online += "{\"name\":\"VideoCDN\",\"url\":\"{localhost}/vcdn\"},";

            if (AppInit.conf.Ashdi.enable)
                online += "{\"name\":\"Ashdi (UKR)\",\"url\":\"{localhost}/ashdi\"},";

            if (AppInit.conf.Eneyida.enable)
                online += "{\"name\":\"Eneyida (UKR)\",\"url\":\"{localhost}/eneyida\"},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Kodik.token))
                online += "{\"name\":\"Kodik\",\"url\":\"{localhost}/kodik\"},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Seasonvar.token) && (serial == -1 || serial == 1))
                online += "{\"name\":\"Seasonvar\",\"url\":\"{localhost}/seasonvar\"},";

            if (AppInit.conf.Lostfilmhd.enable && (serial == -1 || serial == 1))
                online += "{\"name\":\"LostfilmHD\",\"url\":\"{localhost}/lostfilmhd\"},";

            if (AppInit.conf.Collaps.enable)
                online += "{\"name\":\"Collaps\",\"url\":\"{localhost}/collaps\"},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.HDVB.token))
                online += "{\"name\":\"HDVB\",\"url\":\"{localhost}/hdvb\"},";

            if (AppInit.conf.CDNmovies.enable && (serial == -1 || (serial == 1 && !isanime)))
                online += "{\"name\":\"CDNmovies\",\"url\":\"{localhost}/cdnmovies\"},";

            if (serial == -1 || isanime)
            {
                if (AppInit.conf.AnilibriaOnline.enable)
                    online += "{\"name\":\"Anilibria\",\"url\":\"{localhost}/anilibria\"},";

                if (AppInit.conf.Animevost.enable)
                    online += "{\"name\":\"Animevost\",\"url\":\"{localhost}/animevost\"},";

                if (AppInit.conf.Animebesst.enable)
                    online += "{\"name\":\"Animebesst\",\"url\":\"{localhost}/animebesst\"},";

                if (AppInit.conf.AnimeGo.enable)
                    online += "{\"name\":\"AnimeGo\",\"url\":\"{localhost}/animego\"},";

                if (AppInit.conf.AniMedia.enable)
                    online += "{\"name\":\"AniMedia\",\"url\":\"{localhost}/animedia\"},";
            }

            if (AppInit.conf.Kinotochka.enable)
                online += "{\"name\":\"Kinotochka\",\"url\":\"{localhost}/kinotochka\"},";

            if (serial == -1 || serial == 0 || (serial == 1 && !isanime))
            {
                if (AppInit.conf.Kinokrad.enable)
                    online += "{\"name\":\"Kinokrad\",\"url\":\"{localhost}/kinokrad\"},";

                if (AppInit.conf.Kinoprofi.enable)
                    online += "{\"name\":\"Kinoprofi\",\"url\":\"{localhost}/kinoprofi\"},";

                if (AppInit.conf.Redheadsound.enable && (serial == -1 || serial == 0))
                    online += "{\"name\":\"Redheadsound\",\"url\":\"{localhost}/redheadsound\"},";

                if (!string.IsNullOrWhiteSpace(AppInit.conf.VideoAPI.token) && (serial == -1 || serial == 0))
                    online += "{\"name\":\"VideoAPI (ENG)\",\"url\":\"{localhost}/videoapi\"},";
            }

            if (AppInit.conf.IframeVideo.enable && (serial == -1 || serial == 0))
                online += "{\"name\":\"IframeVideo\",\"url\":\"{localhost}/iframevideo\"},";

            #region checkOnlineSearch
            if (AppInit.conf.online.checkOnlineSearch && id > 0)
            {
                string memkey = $"ApiController:checkOnlineSearch:{id}";
                if (!memoryCache.TryGetValue(memkey, out (bool ready, int tasks, string online) cache))
                {
                    memoryCache.Set(memkey, string.Empty, DateTime.Now.AddSeconds(15));

                    var tasks = new List<Task>();
                    var links = new ConcurrentBag<(string code, int index, bool work)>();

                    var match = Regex.Match(online, "(\\{\"name\":\"[^\"]+\",\"url\":\"\\{localhost\\}/([^\"]+)\"\\},)");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
                            tasks.Add(checkSearch(links, tasks, tasks.Count, match.Groups[1].Value, match.Groups[2].Value, id, imdb_id, kinopoisk_id, title, original_title, original_language, source, year, serial));

                        match = match.NextMatch();
                    }

                    if (life)
                        return Content("{\"life\":true}", contentType: "application/javascript; charset=utf-8");

                    await Task.WhenAll(tasks);

                    cache.ready = true;
                    cache.tasks = tasks.Count;
                    cache.online = string.Join("", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code));

                    memoryCache.Set(memkey, cache, DateTime.Now.AddMinutes(10));
                }

                if (life)
                    return Content("{\"life\":true}", contentType: "application/javascript; charset=utf-8");

                online = cache.online;
            }
            #endregion

            return Content($"[{Regex.Replace(online, ",$", "").Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite")}]", contentType: "application/javascript; charset=utf-8");
        }
        #endregion


        #region checkSearch
        async Task checkSearch(ConcurrentBag<(string code, int index, bool work)> links, List<Task> tasks, int index, string code, string uri,
                               int id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, string source, int year, int serial)
        {
            string res = await HttpClient.Get("http://127.0.0.1:" + AppInit.conf.listenport + $"/lite/{uri}?id={id}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&original_language={original_language}&source={source}&year={year}&serial={serial}", timeoutSeconds: 10);

            bool work = !string.IsNullOrWhiteSpace(res) && res.Contains("data-json=");
            links.Add((code.Replace("},", $",\"index\":{index},\"show\":{work.ToString().ToLower()}" + "},"), index, work));

            memoryCache.Set($"ApiController:checkOnlineSearch:{id}", (links.Count == tasks.Count, tasks.Count, string.Join("", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code))), DateTime.Now.AddMinutes(10));
        }
        #endregion
    }
}
