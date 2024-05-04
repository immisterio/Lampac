using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System;
using IO = System.IO;
using Lampac.Engine.CORE;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using Microsoft.Extensions.Caching.Memory;
using Shared.Engine;

namespace Lampac.Controllers
{
    public class TimecodeController : BaseController
    {
        static TimecodeController()
        {
            Directory.CreateDirectory("cache/timecode");
        }

        #region timecode.js
        [HttpGet]
        [Route("timecode.js")]
        public ActionResult timecode()
        {
            return Content(FileCache.ReadAllText("plugins/timecode.js").Replace("{localhost}", host), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("/timecode/all")]
        public ActionResult Get(string card_id, long profile)
        {
            string path = getFilePath(card_id, profile, false);
            if (!IO.File.Exists(path))
                return Json(new { });

            return Json(getData(path));
        }

        [HttpPost]
        [Route("/timecode/add")]
        public ActionResult Set([FromQuery]long profile, [FromQuery]string card_id, [FromForm]string id, [FromForm]string data)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(data))
                return Content("{\"secuses\", false}");

            string path = getFilePath(card_id, profile, true);
            var db = getData(path);

            if (db.ContainsKey(id))
            {
                db[id] = data;
            }
            else
            {
                db.TryAdd(id, data);
            }

            BrotliTo.Compress(path, JsonConvert.SerializeObject(db));
            return Content("{\"secuses\", true}");
        }


        #region getFilePath
        string getFilePath(string card_id, long profile, bool createDirectory)
        {
            string md5key = CrypTo.md5($"{card_id}:{profile}");

            if (createDirectory)
                Directory.CreateDirectory($"cache/timecode/{md5key.Substring(0, 2)}");

            return $"cache/timecode/{md5key.Substring(0, 2)}/{md5key.Substring(2)}";
        }
        #endregion

        #region getData
        Dictionary<string, string> getData(string path)
        {
            if (!memoryCache.TryGetValue($"TimecodeController:{path}", out Dictionary<string, string> data))
            {
                data = IO.File.Exists(path) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(BrotliTo.Decompress(path)) : new Dictionary<string, string>();
                memoryCache.Set($"TimecodeController:{path}", data, DateTime.Now.AddMinutes(10));
            }

            return data;
        }
        #endregion
    }
}