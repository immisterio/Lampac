using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using IO = System.IO;
using Lampac.Engine.CORE;
using System.IO;
using Shared.Engine;
using System.Web;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Lampac.Controllers
{
    public class BackupController : BaseController
    {
        static BackupController()
        {
            Directory.CreateDirectory("cache/backup");
        }

        #region backup.js
        [HttpGet]
        [Route("backup.js")]
        [Route("backup/js/{token}")]
        public ActionResult Index(string token)
        {
            string file = FileCache.ReadAllText("plugins/backup.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("/backup/import")]
        public ActionResult Import(string account_email, string token)
        {
            string path = getFilePath(account_email, token, false);
            if (path == null || !IO.File.Exists(path))
                Content("{\"secuses\": false, \"msg\": \"path\"}", "application/json; charset=utf-8");

            return Json(new { secuses = true, data = JsonConvert.DeserializeObject<Dictionary<string, string>>(BrotliTo.Decompress(path)) });
        }

        [HttpPost]
        [Route("/backup/export")]
        async public Task<ActionResult> Export([FromQuery] string account_email, [FromQuery] string token, IFormFile file)
        {
            if (file == null || file.Length == 0)
                Content("{\"secuses\": false, \"msg\": \"file\"}", "application/json; charset=utf-8");

            string path = getFilePath(account_email, token, true);
            if (path == null)
                Content("{\"secuses\": false, \"msg\": \"path\"}", "application/json; charset=utf-8");

            byte[] array = null;
            using (var memoryStream = new MemoryStream()) {
                await file.CopyToAsync(memoryStream);
                array = memoryStream.ToArray();
            }

            BrotliTo.Compress(path, array);
            return Json(new { secuses = true });
        }


        #region getFilePath
        string getFilePath(string account_email, string token, bool createDirectory)
        {
            string id = token;
            if (string.IsNullOrEmpty(id))
                id = account_email;

            if (string.IsNullOrEmpty(id))
                return null;

            string md5key = CrypTo.md5(id);

            if (createDirectory)
                Directory.CreateDirectory($"cache/backup/{md5key.Substring(0, 2)}");

            return $"cache/backup/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
        }
        #endregion
    }
}