using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;

namespace Lampac.Controllers
{
    public class ApiController : BaseController
    {
        [Route("/")]
        public ActionResult Index()
        {
            if (AppInit.conf.LampaWeb.autoindex && System.IO.File.Exists("wwwroot/lampa-main/index.html"))
                return LocalRedirect("/lampa-main/index.html");

            return Content("api work", contentType: "text/plain; charset=utf-8");
        }

        [HttpGet]
        [Route("lampainit.js")]
        public ActionResult LamInit()
        {
            string file = System.IO.File.ReadAllText("plugins/lampainit.js");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));
            file = file.Replace("{jachost}", AppInit.Host(HttpContext).Replace("https://", "").Replace("http://", ""));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("msx/start.json")]
        public ActionResult MSX()
        {
            string file = System.IO.File.ReadAllText("msx.json");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/json; charset=utf-8");
        }

        [HttpGet]
        [Route("sisi.js")]
        public ActionResult Sisi()
        {
            string file = System.IO.File.ReadAllText("plugins/sisi.js");
            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/sisi");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("online.js")]
        public ActionResult Online()
        {
            string file = System.IO.File.ReadAllText("plugins/online.js");
            file = file.Replace("http://127.0.0.1:9118", AppInit.Host(HttpContext));
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("lite.js")]
        public ActionResult Lite(int serial = -1)
        {
            string file = System.IO.File.ReadAllText("plugins/lite.js");

            string online = string.Empty;

            online += "{name:'Jackett',url:'{localhost}/jac'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.KinoPub.token))
                online += "{name:'KinoPub',url:'{localhost}/kinopub'},";

            if (AppInit.conf.Filmix.enable)
                online += "{name:'Filmix',url:'{localhost}/filmix'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Alloha.token))
                online += "{name:'Alloha',url:'{localhost}/alloha'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Bazon.token))
                online += "{name:'Bazon',url:'{localhost}/bazon'},";

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

            if (!string.IsNullOrWhiteSpace(AppInit.conf.Seasonvar.token) && serial == 1)
                online += "{name:'Seasonvar',url:'{localhost}/seasonvar'},";

            if (AppInit.conf.Lostfilmhd.enable && serial == 1)
                online += "{name:'LostfilmHD',url:'{localhost}/lostfilmhd'},";

            if (AppInit.conf.Collaps.enable)
                online += "{name:'Collaps',url:'{localhost}/collaps'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.HDVB.token))
                online += "{name:'HDVB',url:'{localhost}/hdvb'},";

            if (AppInit.conf.AnimeGo.enable)
                online += "{name:'AnimeGo',url:'{localhost}/animego'},";

            if (AppInit.conf.AnilibriaOnline.enable)
                online += "{name:'Anilibria',url:'{localhost}/anilibria'},";

            if (AppInit.conf.AniMedia.enable)
                online += "{name:'AniMedia',url:'{localhost}/animedia'},";

            if (AppInit.conf.Kinokrad.enable)
                online += "{name:'Kinokrad',url:'{localhost}/kinokrad'},";

            if (AppInit.conf.Kinotochka.enable)
                online += "{name:'Kinotochka',url:'{localhost}/kinotochka'},";

            if (AppInit.conf.Kinoprofi.enable)
                online += "{name:'Kinoprofi',url:'{localhost}/kinoprofi'},";

            if (AppInit.conf.Redheadsound.enable && serial == 0)
                online += "{name:'Redheadsound',url:'{localhost}/redheadsound'},";

            if (!string.IsNullOrWhiteSpace(AppInit.conf.VideoAPI.token) && serial == 0)
                online += "{name:'VideoAPI (ENG)',url:'{localhost}/videoapi'},";

            if (AppInit.conf.IframeVideo.enable && serial == 0)
                online += "{name:'IframeVideo',url:'{localhost}/iframevideo'},";

            file = file.Replace("{online}", online);
            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
    }
}
