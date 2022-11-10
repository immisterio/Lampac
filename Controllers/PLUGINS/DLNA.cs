using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.IO;
using System.Web;
using System.Collections.Generic;
using Lampac.Models.DLNA;
using IO = System.IO;

namespace Lampac.Controllers.PLUGINS
{
    public class DLNAController : BaseController
    {
        [HttpGet]
        [Route("dlna.js")]
        public ActionResult Plugin()
        {
            string file = IO.File.ReadAllText("dlna.js");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }


        [HttpGet]
        [Route("dlna")]
        public JsonResult Index(string path)
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            var playlist = new List<DlnaModel>();

            foreach (string folder in Directory.GetDirectories("dlna/" + path))
            {
                playlist.Add(new DlnaModel() 
                {
                    type = "folder",
                    name = Path.GetFileName(folder),
                    uri = $"{AppInit.Host(HttpContext)}/dlna?path={HttpUtility.UrlEncode(folder.Replace("dlna/", ""))}",
                    path = folder.Replace("dlna/", ""),
                    length = Directory.GetFiles(folder).Length
                });
            }

            foreach (string file in Directory.GetFiles("dlna/" + path))
            {
                playlist.Add(new DlnaModel()
                {
                    type = "file",
                    name = Path.GetFileName(file),
                    uri = $"{AppInit.Host(HttpContext)}/dlna/stream?path={HttpUtility.UrlEncode(file.Replace("dlna/", ""))}",
                    path = file.Replace("dlna/", ""),
                    length = new FileInfo(file).Length
                });
            }

            return Json(playlist);
        }


        [Route("dlna/stream")]
        public ActionResult Stream(string path)
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            return File(IO.File.OpenRead("dlna/" + path), "application/octet-stream", true);
        }


        [HttpGet]
        [Route("dlna/delete")]
        public ActionResult Delete(string path)
        {
            if (!AppInit.conf.dlna)
                return Content(string.Empty);

            try
            {
                IO.File.Delete("dlna/" + path);
            }
            catch { }

            try
            {
                Directory.Delete("dlna/" + path, true);
            }
            catch { }

            return Content(string.Empty);
        }
    }
}
