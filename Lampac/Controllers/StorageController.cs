﻿using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using IO = System.IO;
using Lampac.Engine.CORE;
using System.IO;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using System;
using System.Text.RegularExpressions;

namespace Lampac.Controllers
{
    public class StorageController : BaseController
    {
        static StorageController()
        {
            Directory.CreateDirectory("cache/storage");
        }


        [Route("/storage/get")]
        public ActionResult Get(string path, bool responseInfo)
        {
            string outFile = getFilePath(path, false);
            if (outFile == null || !IO.File.Exists(outFile))
                return Content("{\"success\": false, \"msg\": \"outFile\"}", "application/json; charset=utf-8");

            var file = new FileInfo(outFile);
            var fileInfo = new { file.Name, path = outFile, file.Length, changeTime = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds() };

            if (responseInfo)
                return Json(new { success = true, uid = requestInfo?.user_uid, fileInfo });

            string data = BrotliTo.Decompress(outFile);
            if (data == null)
                data = IO.File.ReadAllText(outFile);

            return Json(new { success = true, uid = requestInfo?.user_uid, fileInfo, data });
        }


        [HttpPost]
        [Route("/storage/set")]
        async public Task<ActionResult> Set([FromQuery]string path)
        {
            if (!AppInit.conf.storage.enable)
                return Content("{\"success\": false, \"msg\": \"disabled\"}", "application/json; charset=utf-8");

            if (HttpContext.Request.ContentLength > AppInit.conf.storage.max_size)
                return Content("{\"success\": false, \"msg\": \"max_size\"}", "application/json; charset=utf-8");

            string outFile = getFilePath(path, true);
            if (outFile == null)
                return Content("{\"success\": false, \"msg\": \"outFile\"}", "application/json; charset=utf-8");

            byte[] array = null;
            using (var memoryStream = new MemoryStream()) {
                await HttpContext.Request.Body.CopyToAsync(memoryStream);
                array = memoryStream.ToArray();
            }

            if (AppInit.conf.storage.brotli)
                BrotliTo.Compress(outFile, array);
            else
                IO.File.WriteAllBytes(outFile, array);  

            var inf = new FileInfo(outFile);

            return Json(new 
            { 
                success = true,
                uid = requestInfo?.user_uid,
                fileInfo = new { inf.Name, path = outFile, inf.Length, changeTime = new DateTimeOffset(inf.LastWriteTimeUtc).ToUnixTimeMilliseconds() }
            });
        }


        #region getFilePath
        string getFilePath(string path, bool createDirectory)
        {
            string id = requestInfo.user_uid;
            if (string.IsNullOrEmpty(id))
                return null;

            string md5key = AppInit.conf.storage.md5name ? CrypTo.md5(id) : Regex.Replace(id, "(\\@|_)", "");

            if (createDirectory)
                Directory.CreateDirectory($"cache/storage/{path}/{md5key.Substring(0, 2)}");

            return $"cache/storage/{path}/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
        }
        #endregion
    }
}