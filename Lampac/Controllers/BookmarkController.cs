using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared;
using Shared.Engine;
using Shared.Models.SQL;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Lampac.Controllers
{
    public class BookmarkController : BaseController
    {
        #region bookmark.js
        [HttpGet]
        [Route("bookmark.js")]
        [Route("bookmark/js/{token}")]
        public ActionResult BookmarkJS(string token)
        {
            if (!AppInit.conf.storage.enable)
                return Content(string.Empty, "application/javascript; charset=utf-8");

            var sb = new StringBuilder(FileCache.ReadAllText("plugins/bookmark.js"));

            sb.Replace("{localhost}", host)
              .Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
        }
        #endregion


        private static readonly string[] BookmarkCategories = new[]
        {
            "history",
            "like",
            "watch",
            "wath",
            "book",
            "look",
            "viewed",
            "scheduled",
            "continued",
            "thrown"
        };

        [HttpGet]
        [Route("/bookmark/list")]
        public ActionResult List()
        {
            var data = GetBookmarksForResponse(SyncUserDb.Read);
            return ContentTo(data.ToString(Formatting.None));
        }

        [HttpPost]
        [Route("/bookmark/add")]
        public async Task<ActionResult> Add()
        {
            if (string.IsNullOrEmpty(requestInfo.user_uid))
                return JsonFailure();

            var payload = await ReadPayloadAsync();
            if (payload?.Card == null)
                return JsonFailure();

            var cardId = payload.ResolveCardId();
            if (cardId == null)
                return JsonFailure();

            string category = NormalizeCategory(payload.Where);

            using (var sqlDb = new SyncUserContext())
            {
                var (entity, data) = LoadBookmarks(sqlDb, requestInfo.user_uid, createIfMissing: true);
                bool changed = false;

                changed |= EnsureCard(data, payload.Card, cardId.Value);

                if (!string.IsNullOrEmpty(category))
                    changed |= AddToCategory(data, category, cardId.Value);

                if (changed)
                    Save(sqlDb, entity, data);
            }

            return JsonSuccess();
        }

        [HttpPost]
        [Route("/bookmark/added")]
        public async Task<ActionResult> Added()
        {
            if (string.IsNullOrEmpty(requestInfo.user_uid))
                return JsonFailure();

            var payload = await ReadPayloadAsync();
            if (payload == null)
                return JsonFailure();

            var cardId = payload.ResolveCardId();
            if (cardId == null)
                return JsonFailure();

            string category = NormalizeCategory(payload.Where);

            using (var sqlDb = new SyncUserContext())
            {
                var (entity, data) = LoadBookmarks(sqlDb, requestInfo.user_uid, createIfMissing: true);
                bool changed = false;

                if (payload.Card != null)
                    changed |= EnsureCard(data, payload.Card, cardId.Value);

                if (!string.IsNullOrEmpty(category))
                    changed |= AddToCategory(data, category, cardId.Value);

                if (changed)
                    Save(sqlDb, entity, data);
            }

            return JsonSuccess();
        }

        [HttpPost]
        [Route("/bookmark/remove")]
        public async Task<ActionResult> Remove()
        {
            if (string.IsNullOrEmpty(requestInfo.user_uid))
                return JsonFailure();

            var payload = await ReadPayloadAsync();
            if (payload == null)
                return JsonFailure();

            var cardId = payload.ResolveCardId();
            if (cardId == null)
                return JsonFailure();

            string category = NormalizeCategory(payload.Where);
            string method = payload.NormalizedMethod;

            using (var sqlDb = new SyncUserContext())
            {
                var (entity, data) = LoadBookmarks(sqlDb, requestInfo.user_uid, createIfMissing: false);
                if (entity == null)
                    return JsonSuccess();

                bool changed = false;

                if (!string.IsNullOrEmpty(category))
                    changed |= RemoveFromCategory(data, category, cardId.Value);

                if (string.Equals(method, "card", StringComparison.Ordinal))
                {
                    changed |= RemoveIdFromAllCategories(data, cardId.Value);
                    changed |= RemoveCard(data, cardId.Value);
                }

                if (changed)
                    Save(sqlDb, entity, data);
            }

            return JsonSuccess();
        }

        JObject GetBookmarksForResponse(SyncUserContext sqlDb)
        {
            if (string.IsNullOrEmpty(requestInfo.user_uid))
                return CreateDefaultBookmarks();

            var entity = sqlDb.bookmarks.AsNoTracking().FirstOrDefault(i => i.user == requestInfo.user_uid);
            var data = entity != null ? DeserializeBookmarks(entity.data) : CreateDefaultBookmarks();
            EnsureDefaultArrays(data);
            return data;
        }

        static (SyncUserBookmarkSqlModel entity, JObject data) LoadBookmarks(SyncUserContext sqlDb, string userUid, bool createIfMissing)
        {
            JObject data = CreateDefaultBookmarks();
            SyncUserBookmarkSqlModel entity = null;

            if (!string.IsNullOrEmpty(userUid))
            {
                entity = sqlDb.bookmarks.FirstOrDefault(i => i.user == userUid);
                if (entity != null && !string.IsNullOrEmpty(entity.data))
                    data = DeserializeBookmarks(entity.data);
            }

            EnsureDefaultArrays(data);

            if (entity == null && createIfMissing && !string.IsNullOrEmpty(userUid))
                entity = new SyncUserBookmarkSqlModel { user = userUid };

            return (entity, data);
        }

        static JObject DeserializeBookmarks(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return CreateDefaultBookmarks();

            try
            {
                var job = JsonConvert.DeserializeObject<JObject>(json) ?? new JObject();
                EnsureDefaultArrays(job);
                return job;
            }
            catch
            {
                return CreateDefaultBookmarks();
            }
        }

        static JObject CreateDefaultBookmarks()
        {
            var obj = new JObject
            {
                ["card"] = new JArray()
            };

            foreach (var category in BookmarkCategories)
                obj[category] = new JArray();

            return obj;
        }

        static void EnsureDefaultArrays(JObject root)
        {
            if (root == null)
                return;

            if (root["card"] is not JArray)
                root["card"] = new JArray();

            foreach (var category in BookmarkCategories)
            {
                if (root[category] is not JArray)
                    root[category] = new JArray();
            }
        }

        static string NormalizeCategory(string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return null;

            var normalized = category.Trim().ToLowerInvariant();
            return BookmarkCategories.Contains(normalized) ? normalized : null;
        }

        static bool EnsureCard(JObject data, JObject card, long id)
        {
            if (data == null || card == null)
                return false;

            var cardArray = GetCardArray(data);
            string idStr = id.ToString(CultureInfo.InvariantCulture);
            var newCard = (JObject)card.DeepClone();

            foreach (var existing in cardArray.Children<JObject>().ToList())
            {
                var token = existing["id"];
                if (token != null && token.ToString() == idStr)
                {
                    if (!JToken.DeepEquals(existing, newCard))
                    {
                        existing.Replace(newCard);
                        return true;
                    }

                    return false;
                }
            }

            cardArray.Add(newCard);
            return true;
        }

        static bool AddToCategory(JObject data, string category, long id)
        {
            if (data == null || string.IsNullOrEmpty(category) || !BookmarkCategories.Contains(category))
                return false;

            var array = GetCategoryArray(data, category);
            string idStr = id.ToString(CultureInfo.InvariantCulture);

            foreach (var token in array)
            {
                if (token.ToString() == idStr)
                    return false;
            }

            array.Add(id);
            return true;
        }

        static bool RemoveFromCategory(JObject data, string category, long id)
        {
            if (data == null || string.IsNullOrEmpty(category) || !BookmarkCategories.Contains(category))
                return false;

            if (data[category] is not JArray array)
                return false;

            return RemoveFromArray(array, id);
        }

        static bool RemoveIdFromAllCategories(JObject data, long id)
        {
            if (data == null)
                return false;

            bool changed = false;

            foreach (var property in data.Properties().ToList())
            {
                if (property.Name == "card")
                    continue;

                if (property.Value is JArray array && RemoveFromArray(array, id))
                    changed = true;
            }

            return changed;
        }

        static bool RemoveCard(JObject data, long id)
        {
            if (data == null)
                return false;

            var cardArray = GetCardArray(data);
            string idStr = id.ToString(CultureInfo.InvariantCulture);

            foreach (var card in cardArray.Children<JObject>().ToList())
            {
                var token = card["id"];
                if (token != null && token.ToString() == idStr)
                {
                    card.Remove();
                    return true;
                }
            }

            return false;
        }

        static JArray GetCardArray(JObject data)
        {
            if (data["card"] is JArray array)
                return array;

            array = new JArray();
            data["card"] = array;
            return array;
        }

        static JArray GetCategoryArray(JObject data, string category)
        {
            if (data[category] is JArray array)
                return array;

            array = new JArray();
            data[category] = array;
            return array;
        }

        static bool RemoveFromArray(JArray array, long id)
        {
            string idStr = id.ToString(CultureInfo.InvariantCulture);

            foreach (var token in array.ToList())
            {
                if (token.ToString() == idStr)
                {
                    token.Remove();
                    return true;
                }
            }

            return false;
        }

        static void Save(SyncUserContext sqlDb, SyncUserBookmarkSqlModel entity, JObject data)
        {
            if (entity == null)
                return;

            entity.data = data.ToString(Formatting.None);
            entity.updated = DateTime.UtcNow;

            if (entity.Id == 0)
                sqlDb.bookmarks.Add(entity);
            else
                sqlDb.bookmarks.Update(entity);

            sqlDb.SaveChanges();
        }

        JsonResult JsonSuccess() => Json(new { success = true });

        JsonResult JsonFailure() => Json(new { success = false });

        async Task<BookmarkEventPayload> ReadPayloadAsync()
        {
            var payload = new BookmarkEventPayload();

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                payload.Method = form["method"];
                payload.Where = form["where"];
                payload.CardIdRaw = form["id"];
                if (string.IsNullOrEmpty(payload.CardIdRaw))
                    payload.CardIdRaw = form["card_id"];

                var cardJson = form["card"];
                if (!string.IsNullOrWhiteSpace(cardJson))
                    payload.Card = ParseCardString(cardJson);

                return payload;
            }

            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true))
            {
                string body = await reader.ReadToEndAsync();
                if (string.IsNullOrWhiteSpace(body))
                    return payload;

                try
                {
                    var job = JsonConvert.DeserializeObject<JObject>(body);
                    if (job == null)
                        return payload;

                    payload.Method = job.Value<string>("method");
                    payload.Where = job.Value<string>("where") ?? job.Value<string>("list");
                    payload.CardIdRaw = job.Value<string>("id") ?? job.Value<string>("card_id");

                    if (job.TryGetValue("card", out var cardToken))
                        payload.Card = ConvertToCard(cardToken);
                }
                catch
                {
                }
            }

            return payload;
        }

        static JObject ConvertToCard(JToken token)
        {
            if (token == null)
                return null;

            if (token.Type == JTokenType.Object)
                return (JObject)token;

            if (token.Type == JTokenType.String)
                return ParseCardString(token.Value<string>());

            return null;
        }

        static JObject ParseCardString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            try
            {
                return JsonConvert.DeserializeObject<JObject>(value);
            }
            catch
            {
                return null;
            }
        }

        sealed class BookmarkEventPayload
        {
            public string Method { get; set; }

            public string Where { get; set; }

            public JObject Card { get; set; }

            public string CardIdRaw { get; set; }

            public string NormalizedMethod => string.IsNullOrWhiteSpace(Method) ? null : Method.Trim().ToLowerInvariant();

            public long? ResolveCardId()
            {
                if (!string.IsNullOrWhiteSpace(CardIdRaw) && long.TryParse(CardIdRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                    return parsed;

                var token = Card?["id"];
                if (token != null)
                {
                    if (token.Type == JTokenType.Integer)
                        return token.Value<long>();

                    if (long.TryParse(token.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                        return parsed;
                }

                return null;
            }
        }
    }
}
