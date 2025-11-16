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

            Dictionary<string, string> timecodes = SyncUserDb.Read.timecodes
                .AsNoTracking()
                .Where(i => i.user == userId && i.card == card_id)
                .ToDictionary(i => i.item, i => i.data);

            if (timecodes.Count == 0)
                return Json(new { });

            return Json(timecodes);
        }

        [HttpPost]
        [Route("/timecode/add")]
        public ActionResult Set([FromQuery] string card_id, [FromForm] string id, [FromForm] string data)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(data))
                return Content("{\"secuses\": false}", "application/json; charset=utf-8");

            if (string.IsNullOrEmpty(card_id))
                return Content("{\"secuses\": false}", "application/json; charset=utf-8");

            string userId = getUserid(requestInfo, HttpContext);

            bool secuses;

            using (var sqlDb = new SyncUserContext())
            {
                sqlDb.timecodes
                    .Where(i => i.user == userId && i.card == card_id && i.item == id)
                    .ExecuteDelete();

                var entity = new SyncUserTimecodeSqlModel
                {
                    user = userId,
                    card = card_id,
                    item = id,
                    data = data,
                    updated = DateTime.UtcNow
                };

                sqlDb.timecodes.Add(entity);
                secuses = sqlDb.SaveChanges() > 0;
            }

            return Json(new { secuses });
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