using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;

namespace Lampac.Controllers
{
    public class ApiController : BaseController
    {
        [HttpGet]
        [Route("sisi.js")]
        public ActionResult Sisi()
        {
            string file = System.IO.File.ReadAllText("sisi.js");
            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/sisi");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("lite.js")]
        public ActionResult Lite()
        {
            string file = System.IO.File.ReadAllText("lite.js");
            file = file.Replace("{localhost}", $"{AppInit.Host(HttpContext)}/lite");

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }

        [HttpGet]
        [Route("online.js")]
        async public Task<ActionResult> Online()
        {
            string cachekey = "online.js";
            if (!HtmlCache.Read(cachekey, out string cachetxt))
            {
                string txt = await HttpClient.Get("https://pastebin.com/raw/Qkm6WFtZ");
                if (txt == null || !txt.Contains("Lampa.Reguest()"))
                    txt = await HttpClient.Get("http://jin.energy/online.js?v=1666454579");

                if (txt != null && txt.Contains("Lampa.Reguest()"))
                {
                    cachetxt = txt;
                    HtmlCache.Write(cachekey, txt);
                }

                if (cachetxt == null)
                    return OnError("cachetxt");
            }

            return Content(cachetxt, contentType: "application/javascript; charset=utf-8");
        }
    }
}
