using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using System;
using IO = System.IO;
using Lampac.Engine.CORE;
using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;

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
        async public Task<ActionResult> timecode()
        {
            if (!memoryCache.TryGetValue("ApiController:timecode.js", out string file))
            {
                file = await IO.File.ReadAllTextAsync("plugins/timecode.js");
                memoryCache.Set("ApiController:timecode.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file.Replace("{localhost}", host), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("/timecode/all")]
        async public Task<ActionResult> Get(string card_id, long profile)
        {
            string path = getFilePath(card_id, profile, false);
            if (!IO.File.Exists(path))
                return Json(new { });

            return Json(await getData(path));
        }

        [Route("/timecode/add")]
        async public Task<ActionResult> Set(long profile, string card_id, string id, string data)
        {
            string path = getFilePath(card_id, profile, true);
            var db = await getData(path);

            if (db.ContainsKey(id))
            {
                db[id] = data;
            }
            else
            {
                db.TryAdd(id, data);
            }

            await IO.File.WriteAllBytesAsync(path, BrotliTo.Compress(JsonConvert.SerializeObject(db)));
            return Content(string.Empty);
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
        async ValueTask<Dictionary<string, string>> getData(string path)
        {
            if (!memoryCache.TryGetValue($"TimecodeController:{path}", out Dictionary<string, string> data))
            {
                data = IO.File.Exists(path) ? JsonConvert.DeserializeObject<Dictionary<string, string>>(BrotliTo.Decompress(await IO.File.ReadAllBytesAsync(path))) : new Dictionary<string, string>();
                memoryCache.Set($"TimecodeController:{path}", data, DateTime.Now.AddMinutes(10));
            }

            return data;
        }
        #endregion
    }
}