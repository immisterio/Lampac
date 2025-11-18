using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using Shared.Engine;
using Shared.Models;
using Shared.Models.SQL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers
{
    public class TimecodeController : BaseController
    {
        #region timecode.js
        [HttpGet]
        [AllowAnonymous]
        [Route("timecode.js")]
        [Route("timecode/js/{token}")]
        public ActionResult timecode(string token)
        {
            string file = FileCache.ReadAllText("plugins/timecode.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        [Route("/timecode/all")]
        public ActionResult Get(string card_id)
        {
            if (string.IsNullOrEmpty(card_id))
                return Json(new { });

            string userId = getUserid(requestInfo, HttpContext);

            Dictionary<string, string> timecodes = null;

            using (var sqlDb = new SyncUserContext())
            {
                sqlDb.timecodes
                    .AsNoTracking()
                    .Where(i => i.user == userId && i.card == card_id)
                    .ToDictionary(i => i.item, i => i.data);
            }

            if (timecodes == null || timecodes.Count == 0)
                return Json(new { });

            return Json(timecodes);
        }

        [HttpPost]
        [Route("/timecode/add")]
        async public Task<ActionResult> Set([FromQuery] string card_id, [FromForm] string id, [FromForm] string data)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(data))
                return ContentTo("{\"success\": false}");

            if (string.IsNullOrEmpty(card_id))
                return ContentTo("{\"success\": false}");

            string userId = getUserid(requestInfo, HttpContext);

            bool success = false;

            try
            {
                await SyncUserContext.semaphore.WaitAsync(TimeSpan.FromSeconds(30));

                using (var sqlDb = new SyncUserContext())
                {
                    sqlDb.timecodes
                        .Where(i => i.user == userId && i.card == card_id && i.item == id)
                        .ExecuteDelete();

                    sqlDb.timecodes.Add(new SyncUserTimecodeSqlModel
                    {
                        user = userId,
                        card = card_id,
                        item = id,
                        data = data,
                        updated = DateTime.UtcNow
                    });

                    success = sqlDb.SaveChanges() > 0;
                }
            }
            catch { }
            finally
            {
                SyncUserContext.semaphore.Release();
            }

            return ContentTo($"{{\"success\": {success.ToString().ToLower()}}}");
        }


        static string getUserid(RequestModel requestInfo, HttpContext httpContext)
        {
            string user_id = requestInfo.user_uid;

            if (httpContext.Request.Query.TryGetValue("profile_id", out var profile_id) && !string.IsNullOrEmpty(profile_id) && profile_id != "0")
                return $"{user_id}_{profile_id}";

            return user_id;
        }
    }
}