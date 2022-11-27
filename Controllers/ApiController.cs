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

namespace Lampac.Controllers
{
    public class ApiController : BaseController
    {
        [Route("/")]
        public ActionResult Index()
        {
            if (AppInit.conf.LampaWeb.autoindex && System.IO.File.Exists($"wwwroot/{AppInit.conf.LampaWeb.index}"))
                return LocalRedirect($"/{AppInit.conf.LampaWeb.index}");

            return Content("api work", contentType: "text/plain; charset=utf-8");
        }

        [HttpGet]
        [Route("lampainit.js")]
        public ActionResult LamInit(bool lite)
        {
            if (!memoryCache.TryGetValue($"ApiController:lampainit.js:{lite}", out string file))
            {
                file = System.IO.File.ReadAllText($"plugins/{(lite ? "liteinit" : "lampainit")}.js");
                memoryCache.Set($"ApiController:lampainit.js:{lite}", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", AppInit.Host(HttpContext));
            file = file.Replace("{jachost}", AppInit.Host(HttpContext).Replace("https://", "").Replace("http://", ""));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("msx/start.json")]
        public ActionResult MSX()
        {
            if (!memoryCache.TryGetValue("ApiController:msx.json", out string file))
            {
                file = System.IO.File.ReadAllText("msx.json");
                memoryCache.Set("ApiController:msx.json", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", AppInit.Host(HttpContext));
            return Content(file, contentType: "application/json; charset=utf-8");
        }

        [HttpGet]
        [Route("sisi.js")]
        public ActionResult Sisi()
        {
            if (!memoryCache.TryGetValue("ApiController:sisi.js", out string file))
            {
                file = System.IO.File.ReadAllText("plugins/sisi.js");
                memoryCache.Set("ApiController:sisi.js", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/sisi");
            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("online.js")]
        public ActionResult Online()
        {
            if (!memoryCache.TryGetValue("ApiController:online.js", out string file))
            {
                file = System.IO.File.ReadAllText("plugins/online.js");
                memoryCache.Set("ApiController:online.js", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("http://127.0.0.1:9118", AppInit.Host(HttpContext));
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("lite.js")]
        [Route("lite/events")]
        async public Task<ActionResult> Lite(int id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, int year, string source, int serial = -1)
        {
            string online = string.Empty;
            bool isanime = original_language == "ja";

            if (HttpContext.Request.Path.Value.StartsWith("/lite/events") && AppInit.conf.litejac)
                online += "{name:'Jackett',url:'{localhost}/jac'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.KinoPub.token))
                online += "{name:'KinoPub',url:'{localhost}/kinopub'},";

            if (AppInit.conf.Filmix.enable)
                online += "{name:'Filmix',url:'{localhost}/filmix'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Alloha.token))
                online += "{name:'Alloha',url:'{localhost}/alloha'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Bazon.token))
                online += "{name:'Bazon',url:'{localhost}/bazon'},";

            if (AppInit.conf.VideoDB.enable)
                online += "{name:'VideoDB',url:'{localhost}/videodb'},";

            if (AppInit.conf.Zetflix.enable)
                online += "{name:'Zetflix',url:'{localhost}/zetflix'},";

            if (AppInit.conf.Kinobase.enable)
                online += "{name:'Kinobase',url:'{localhost}/kinobase'},";

            if (AppInit.conf.Rezka.enable)
                online += "{name:'HDRezka',url:'{localhost}/rezka'},";

            if (AppInit.conf.VCDN.enable)
                online += "{name:'VideoCDN',url:'{localhost}/vcdn'},";

            if (AppInit.conf.Ashdi.enable)
                online += "{name:'Ashdi (UKR)',url:'{localhost}/ashdi'},";

            if (AppInit.conf.Eneyida.enable)
                online += "{name:'Eneyida (UKR)',url:'{localhost}/eneyida'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Kodik.token))
                online += "{name:'Kodik',url:'{localhost}/kodik'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Seasonvar.token) && (serial == -1 || serial == 1))
                online += "{name:'Seasonvar',url:'{localhost}/seasonvar'},";

            if (AppInit.conf.Lostfilmhd.enable && (serial == -1 || serial == 1))
                online += "{name:'LostfilmHD',url:'{localhost}/lostfilmhd'},";

            if (AppInit.conf.Collaps.enable)
                online += "{name:'Collaps',url:'{localhost}/collaps'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.HDVB.token))
                online += "{name:'HDVB',url:'{localhost}/hdvb'},";

            if (AppInit.conf.CDNmovies.enable && (serial == -1 || (serial == 1 && !isanime)))
                online += "{name:'CDNmovies',url:'{localhost}/cdnmovies'},";

            if (serial == -1 || isanime)
            {
                if (AppInit.conf.AnilibriaOnline.enable)
                    online += "{name:'Anilibria',url:'{localhost}/anilibria'},";

                if (AppInit.conf.Animevost.enable)
                    online += "{name:'Animevost',url:'{localhost}/animevost'},";

                if (AppInit.conf.Animebesst.enable)
                    online += "{name:'Animebesst',url:'{localhost}/animebesst'},";

                if (AppInit.conf.AnimeGo.enable)
                    online += "{name:'AnimeGo',url:'{localhost}/animego'},";

                if (AppInit.conf.AniMedia.enable)
                    online += "{name:'AniMedia',url:'{localhost}/animedia'},";
            }

            if (AppInit.conf.Kinotochka.enable)
                online += "{name:'Kinotochka',url:'{localhost}/kinotochka'},";

            if (serial == -1 || serial == 0 || (serial == 1 && !isanime))
            {
                if (AppInit.conf.Kinokrad.enable)
                    online += "{name:'Kinokrad',url:'{localhost}/kinokrad'},";

                if (AppInit.conf.Kinoprofi.enable)
                    online += "{name:'Kinoprofi',url:'{localhost}/kinoprofi'},";

                if (AppInit.conf.Redheadsound.enable && (serial == -1 || serial == 0))
                    online += "{name:'Redheadsound',url:'{localhost}/redheadsound'},";

                if (!string.IsNullOrWhiteSpace(AppInit.conf.VideoAPI.token) && (serial == -1 || serial == 0))
                    online += "{name:'VideoAPI (ENG)',url:'{localhost}/videoapi'},";
            }

            if (AppInit.conf.IframeVideo.enable && (serial == -1 || serial == 0))
                online += "{name:'IframeVideo',url:'{localhost}/iframevideo'},";

            #region checkOnlineSearch
            if (AppInit.conf.checkOnlineSearch && id > 0)
            {
                string memkey = $"ApiController:checkOnlineSearch:{id}";
                if (!memoryCache.TryGetValue(memkey, out string newonline))
                {
                    var tasks = new List<Task>();
                    var links = new ConcurrentBag<(string code, int index, bool work)>();

                    var match = Regex.Match(online, "(\\{name:'[^']+',url:'\\{localhost\\}/([^']+)'\\},)");
                    while (match.Success)
                    {
                        if (!string.IsNullOrWhiteSpace(match.Groups[2].Value))
                            tasks.Add(checkSearch(links, tasks.Count, match.Groups[1].Value, match.Groups[2].Value, id, imdb_id, kinopoisk_id, title, original_title, original_language, source, year, serial));

                        match = match.NextMatch();
                    }

                    await Task.WhenAll(tasks);
                    newonline = online = string.Join("", links.OrderByDescending(i => i.work).ThenBy(i => i.index).Select(i => i.code));

                    memoryCache.Set(memkey, newonline, DateTime.Now.AddMinutes(10));
                }

                online = newonline;
            }
            #endregion

            if (HttpContext.Request.Path.Value.StartsWith("/lite/events"))
            {
                string events = online.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite");
                events = events.Replace("'", "\"").Replace("{", "{\"").Replace(":\"", "\":\"").Replace("\",", "\",\"").Replace("\"show:", "\"show\":");

                return Content($"[{Regex.Replace(events, ",$", "")}]", contentType: "application/javascript; charset=utf-8");
            }

            if (!memoryCache.TryGetValue($"ApiController:lite.js:{id > 0}", out string file))
            {
                if (id > 0)
                    file = "var append = [{online}]";
                else
                    file = await System.IO.File.ReadAllTextAsync("plugins/lite.js");

                memoryCache.Set($"ApiController:lite.js:{id > 0}", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{online}", online);
            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }


        #region checkSearch
        async Task checkSearch(ConcurrentBag<(string code, int index, bool work)> links, int index, string code, string uri,
                               int id, string imdb_id, long kinopoisk_id, string title, string original_title, string original_language, string source, int year, int serial)
        {
            string res = await HttpClient.Get($"{AppInit.Host(HttpContext)}/lite/{uri}?id={id}&imdb_id={imdb_id}&kinopoisk_id={kinopoisk_id}&title={HttpUtility.UrlEncode(title)}&original_title={HttpUtility.UrlEncode(original_title)}&original_language={original_language}&source={source}&year={year}&serial={serial}", timeoutSeconds: 10);

            bool work = !string.IsNullOrWhiteSpace(res) && res.Contains("data-json=");
            links.Add((code.Replace("},", ",show:" + work.ToString().ToLower()+ "},"), index, work));
        }
        #endregion
    }
}
