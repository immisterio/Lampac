using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using Shared.Engine;
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
        async public Task<ActionResult> Get(string card_id)
        {
            if (string.IsNullOrEmpty(card_id))
                return Json(new { });

            using (var db = new SyncUserContext())
            {
                Dictionary<string, string> timecodes = await db.timecodes
                    .AsNoTracking()
                    .Where(i => i.user == requestInfo.user_uid && i.card == card_id)
                    .ToDictionaryAsync(i => i.item, i => i.data);

                if (timecodes.Count == 0)
                    return Json(new { });

                return Json(timecodes);
            }
        }

        [HttpPost]
        [Route("/timecode/add")]
        async public Task<ActionResult> Set([FromQuery] string card_id, [FromForm] string id, [FromForm] string data)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(data))
                return Content("{\"secuses\": false}", "application/json; charset=utf-8");

            if (string.IsNullOrEmpty(card_id))
                return Content("{\"secuses\": false}", "application/json; charset=utf-8");

            using (var db = new SyncUserContext())
            {
                var entity = await db.timecodes
                    .FirstOrDefaultAsync(i => i.user == requestInfo.user_uid && i.card == card_id && i.item == id);

                if (entity == null)
                {
                    entity = new SyncUserTimecodeSqlModel
                    {
                        user = requestInfo.user_uid,
                        card = card_id,
                        item = id,
                        data = data,
                        updated = DateTime.UtcNow
                    };

                    db.timecodes.Add(entity);
                }
                else
                {
                    entity.data = data;
                    entity.updated = DateTime.UtcNow;
                    db.timecodes.Update(entity);
                }

                await db.SaveChangesAsync();
            }

            return Json(new { secuses = true });
        }
    }
}