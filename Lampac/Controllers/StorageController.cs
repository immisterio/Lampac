using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using IO = System.IO;
using Lampac.Engine.CORE;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;

namespace Lampac.Controllers
{
    public class StorageController : BaseController
    {
        static StorageController()
        {
            Directory.CreateDirectory("cache/storage");
        }


        [Route("/storage/get")]
        public ActionResult Get(string path, string account_email, string token, string uid, bool responseInfo)
        {
            string outFile = getFilePath(path, account_email, token, uid, false);
            if (outFile == null || !IO.File.Exists(outFile))
                Content("{\"success\": false, \"msg\": \"outFile\"}", "application/json; charset=utf-8");

            var file = new FileInfo(outFile);
            var info = new { file.Name, file.Length, changeTime = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds(), outFile };

            if (responseInfo)
                return Json(new { success = true, info });

            return Json(new { success = true, info, data = BrotliTo.Decompress(outFile) });
        }


        [HttpPost]
        [Route("/storage/set")]
        async public Task<ActionResult> Set([FromQuery]string path, [FromQuery]string account_email, [FromQuery]string token, [FromQuery]string uid, IFormFile file)
        {
            if (!AppInit.conf.storage.enable)
                Content("{\"success\": false, \"msg\": \"disabled\"}", "application/json; charset=utf-8");

            if (file == null || file.Length == 0 || file.Length > AppInit.conf.storage.max_size)
                Content("{\"success\": false, \"msg\": \"file\"}", "application/json; charset=utf-8");

            string outFile = getFilePath(path, account_email, token, uid, true);
            if (outFile == null)
                Content("{\"success\": false, \"msg\": \"outFile\"}", "application/json; charset=utf-8");

            byte[] array = null;
            using (var memoryStream = new MemoryStream()) {
                await file.CopyToAsync(memoryStream);
                array = memoryStream.ToArray();
            }

            BrotliTo.Compress(outFile, array);
            var inf = new FileInfo(outFile);

            return Json(new { success = true, info = new { inf.Name, inf.Length, changeTime = new DateTimeOffset(inf.LastWriteTimeUtc).ToUnixTimeMilliseconds(), outFile } });
        }


        #region getFilePath
        string getFilePath(string path, string account_email, string token, string uid, bool createDirectory)
        {
            string id = token;
            if (string.IsNullOrEmpty(id))
            {
                id = account_email;
                if (string.IsNullOrEmpty(id))
                    id = uid;
            }

            if (string.IsNullOrEmpty(id))
                return null;

            string md5key = CrypTo.md5(id);

            if (createDirectory)
                Directory.CreateDirectory($"cache/storage/{path}/{md5key.Substring(0, 2)}");

            return $"cache/storage/{path}/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
        }
        #endregion
    }
}