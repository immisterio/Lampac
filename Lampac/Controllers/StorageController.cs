using Lampac.Engine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using IO = System.IO;

namespace Lampac.Controllers
{
    public class StorageController : BaseController
    {
        #region StorageController
        static readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks = new();

        static StorageController()
        {
            Directory.CreateDirectory("database/storage");
            Directory.CreateDirectory("database/storage/temp");
        }
        #endregion

        #region Get
        [Route("/storage/get")]
        public ActionResult Get(string path, string pathfile, bool responseInfo)
        {
            string outFile = getFilePath(path, pathfile, false);
            if (outFile == null || !IO.File.Exists(outFile))
                return Content("{\"success\": false, \"msg\": \"outFile\"}", "application/json; charset=utf-8");

            var file = new FileInfo(outFile);
            var fileInfo = new { file.Name, path = outFile, file.Length, changeTime = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds() };

            if (responseInfo)
                return Json(new { success = true, uid = requestInfo.user_uid, fileInfo });

            string data = AppInit.conf.storage.brotli ? BrotliTo.Decompress(outFile) : IO.File.ReadAllText(outFile);

            return Json(new { success = true, uid = requestInfo.user_uid, fileInfo, data });
        }
        #endregion

        #region Set
        [HttpPost]
        [Route("/storage/set")]
        async public Task<ActionResult> Set([FromQuery]string path, [FromQuery]string pathfile, [FromQuery]string connectionId, [FromQuery]string events)
        {
            if (!AppInit.conf.storage.enable)
                return ContentTo("{\"success\": false, \"msg\": \"disabled\"}");

            if (HttpContext.Request.ContentLength > AppInit.conf.storage.max_size)
                return ContentTo("{\"success\": false, \"msg\": \"max_size\"}");

            string outFile = getFilePath(path, pathfile, true);
            if (outFile == null)
                return ContentTo("{\"success\": false, \"msg\": \"outFile\"}");

            byte[] array = null;
            using (var memoryStream = new MemoryStream()) 
            {
                try
                {
                    await HttpContext.Request.Body.CopyToAsync(memoryStream);
                    array = memoryStream.ToArray();
                }
                catch 
                {
                    HttpContext.Response.StatusCode = 500;
                    return ContentTo("{\"success\": false, \"msg\": \"Request.Body.CopyToAsync\"}");
                }
            }

            var fileLock = _fileLocks.GetOrAdd(outFile, _ => new SemaphoreSlim(1, 1));

            try
            {
                await fileLock.WaitAsync(TimeSpan.FromMinutes(1));

                if (AppInit.conf.storage.brotli)
                    BrotliTo.Compress(outFile, array);
                else
                {
                    using (var fileStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        fileStream.Write(array, 0, array.Length);
                }
            }
            catch
            {
                HttpContext.Response.StatusCode = 500;
                return ContentTo("{\"success\": false, \"msg\": \"fileLock\"}");
            }
            finally
            {
                try
                {
                    fileLock.Release();
                }
                finally
                {
                    if (fileLock.CurrentCount == 1)
                        _fileLocks.TryRemove(outFile, out _);
                }
            }

            #region events
            if (!string.IsNullOrEmpty(events))
            {
                try
                {
                    var json = JsonConvert.DeserializeObject<JObject>(CrypTo.DecodeBase64(events));
                    _ = soks.SendEvents(json.Value<string>("connectionId"), requestInfo.user_uid, json.Value<string>("name"), json.Value<string>("data")).ConfigureAwait(false);
                    _ = nws.SendEvents(json.Value<string>("connectionId"), requestInfo.user_uid, json.Value<string>("name"), json.Value<string>("data")).ConfigureAwait(false);
                }
                catch { }
            }
            else
            {
                string edata = JsonConvert.SerializeObject(new { path, pathfile });
                _ = nws.SendEvents(connectionId, requestInfo.user_uid, "storage", edata).ConfigureAwait(false);
            }
            #endregion

            var inf = new FileInfo(outFile);

            return Json(new 
            { 
                success = true,
                uid = requestInfo.user_uid,
                fileInfo = new { inf.Name, path = outFile, inf.Length, changeTime = new DateTimeOffset(inf.LastWriteTimeUtc).ToUnixTimeMilliseconds() }
            });
        }
        #endregion

        #region TempGet
        [HttpGet]
        [Route("/storage/temp/{key}")]
        public ActionResult TempGet(string key, bool responseInfo)
        {
            string outFile = getFilePath("temp", null, false, user_uid: key);
            if (outFile == null || !IO.File.Exists(outFile))
                return Content("{\"success\": false, \"msg\": \"outFile\"}", "application/json; charset=utf-8");

            var file = new FileInfo(outFile);
            var fileInfo = new { file.Name, path = outFile, file.Length, changeTime = new DateTimeOffset(file.LastWriteTimeUtc).ToUnixTimeMilliseconds() };

            if (responseInfo)
                return Json(new { success = true, uid = requestInfo.user_uid, fileInfo });

            string data = AppInit.conf.storage.brotli ? BrotliTo.Decompress(outFile) : IO.File.ReadAllText(outFile);

            return Json(new { success = true, uid = requestInfo.user_uid, fileInfo, data });
        }
        #endregion

        #region TempSet
        [HttpPost]
        [Route("/storage/temp/{key}")]
        async public Task<ActionResult> TempSet(string key)
        {
            if (!AppInit.conf.storage.enable)
                return ContentTo("{\"success\": false, \"msg\": \"disabled\"}");

            if (HttpContext.Request.ContentLength > AppInit.conf.storage.max_size)
                return ContentTo("{\"success\": false, \"msg\": \"max_size\"}");

            string outFile = getFilePath("temp", null, true, user_uid: key);
            if (outFile == null)
                return ContentTo("{\"success\": false, \"msg\": \"outFile\"}");

            byte[] array = null;
            using (var memoryStream = new MemoryStream())
            {
                try
                {
                    await HttpContext.Request.Body.CopyToAsync(memoryStream);
                    array = memoryStream.ToArray();
                }
                catch 
                {
                    HttpContext.Response.StatusCode = 500;
                    return ContentTo("{\"success\": false, \"msg\": \"Request.Body.CopyToAsync\"}");
                }
            }

            var fileLock = _fileLocks.GetOrAdd(outFile, _ => new SemaphoreSlim(1, 1));

            try
            {
                await fileLock.WaitAsync(TimeSpan.FromMinutes(1));

                if (AppInit.conf.storage.brotli)
                    BrotliTo.Compress(outFile, array);
                else
                {
                    using (var fileStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        fileStream.Write(array, 0, array.Length);
                }
            }
            catch
            {
                HttpContext.Response.StatusCode = 500;
                return ContentTo("{\"success\": false, \"msg\": \"fileLock\"}");
            }
            finally
            {
                fileLock.Release();

                if (fileLock.CurrentCount == 1)
                    _fileLocks.TryRemove(outFile, out _);
            }

            var inf = new FileInfo(outFile);

            return Json(new
            {
                success = true,
                uid = requestInfo.user_uid,
                fileInfo = new { inf.Name, path = outFile, inf.Length, changeTime = new DateTimeOffset(inf.LastWriteTimeUtc).ToUnixTimeMilliseconds() }
            });
        }
        #endregion


        #region getFilePath
        string getFilePath(string path, string pathfile, bool createDirectory, string user_uid = null)
        {
            if (path == "temp" && string.IsNullOrEmpty(user_uid))
                return null;

            string id = user_uid ?? requestInfo.user_uid;
            if (string.IsNullOrEmpty(id))
                return null;

            id += pathfile;
            string md5key = AppInit.conf.storage.md5name ? CrypTo.md5(id) : Regex.Replace(id, "(\\@|_)", "");

            if (path == "temp")
            {
                return $"database/storage/{path}/{md5key}";
            }
            else
            {
                if (createDirectory)
                    Directory.CreateDirectory($"database/storage/{path}/{md5key.Substring(0, 2)}");

                return $"database/storage/{path}/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
            }
        }
        #endregion
    }
}