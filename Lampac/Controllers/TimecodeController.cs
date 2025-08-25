using Microsoft.AspNetCore.Mvc;
using Shared;
using Shared.Engine;
using Shared.Models.Base;
using System.Collections.Generic;
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
        public ActionResult Get(string card_id)
        {
            var doc = CollectionDb.sync_users.FindById(requestInfo.user_uid);

            if (doc == null || !doc.timecodes.ContainsKey(card_id))
                return Json(new { });

            return Json(doc.timecodes[card_id]);
        }

        [HttpPost]
        [Route("/timecode/add")]
        public ActionResult Set([FromQuery] string card_id, [FromForm] string id, [FromForm] string data)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(data))
                return Content("{\"secuses\": false}", "application/json; charset=utf-8");

            var collection = CollectionDb.sync_users;
            var doc = collection.FindById(requestInfo.user_uid);
            if (doc == null)
            {
                collection.Insert(new UserSync
                {
                    id = requestInfo.user_uid,
                    timecodes = new Dictionary<string, Dictionary<string, string>>() 
                    { 
                        [card_id] = new Dictionary<string, string>() { [id] = data } 
                    }
                });
            }
            else
            {
                if (!doc.timecodes.ContainsKey(card_id))
                    doc.timecodes.Add(card_id, new Dictionary<string, string>());

                var card = doc.timecodes[card_id];
                card[id] = data;

                collection.Update(doc);
            }

            return Json(new { secuses = true });
        }
    }
}