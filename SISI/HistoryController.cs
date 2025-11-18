using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Shared.Models.SQL;
using System.Web;

namespace SISI
{
    public class HistoryController : BaseSisiController
    {
        [Route("sisi/historys")]
        public ActionResult List(int pg = 1, int pageSize = 36)
        {
            string md5user = getuser();
            if (md5user == null || !AppInit.conf.sisi.history.enable)
                return OnError("access denied");

            #region historys
            var historys = new List<PlaylistItem>();
            var historysQuery = new List<SisiHistorySqlModel>();

            using (var sqlDb = new SisiContext())
            {
                historysQuery = sqlDb.historys
                    .AsNoTracking()
                    .Where(i => i.user == md5user)
                    .Take(pageSize * 20)
                    .ToList();
            }

            int total_pages = Math.Max(0, historysQuery.Count / pageSize) + 1;

            var items = historysQuery
                .OrderByDescending(i => i.created)
                .Skip((pg * pageSize) - pageSize)
                .Take(pageSize);

            if (items.Any())
            {
                foreach (var json in items.Select(i => i.json))
                {
                    if (string.IsNullOrEmpty(json))
                        continue;

                    try
                    {
                        var history = JsonConvert.DeserializeObject<PlaylistItem>(json);
                        if (history != null)
                            historys.Add(history);
                    }
                    catch { }
                }
            }
            #endregion

            #region getvideLink
            string getvideLink(PlaylistItem pl)
            {
                if (pl.bookmark.site is "phub" or "phubprem")
                    return $"{host}/{pl.bookmark.site}/vidosik?vkey={HttpUtility.UrlEncode(pl.bookmark.href)}";

                return $"{host}/{pl.bookmark.site}/vidosik?uri={HttpUtility.UrlEncode(pl.bookmark.href)}";
            }
            #endregion

            string localhost = $"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}";

            return new JsonResult(new
            {
                list = historys.Select(pl => new
                {
                    pl.name,
                    video = getvideLink(pl),
                    picture = HostImgProxy(pl.bookmark.image, plugin: pl.bookmark.site),
                    pl.time,
                    pl.json,
                    related = pl.related || Regex.IsMatch(pl.bookmark.site, "^(elo|epr|fph|phub|sbg|xmr|xnx|xds)"),
                    pl.quality,
                    pl.preview,
                    pl.model,
                    pl.bookmark,
                    pl.history_uid
                }).ToArray(),
                total_pages
            });
        }


        [HttpPost]
        [Route("sisi/history/add")]
        async public Task<ActionResult> Add([FromBody] PlaylistItem data)
        {
            string md5user = getuser();
            if (md5user == null || !AppInit.conf.sisi.history.enable || data == null || string.IsNullOrEmpty(data?.bookmark?.site) || string.IsNullOrEmpty(data?.bookmark?.href))
                return OnError("access denied");

            string uid = CrypTo.md5($"{data.bookmark.site}:{data.bookmark.href}");

            using (var sqlDb = new SisiContext())
            {
                if (!sqlDb.historys.AsNoTracking().Any(i => i.user == md5user && i.uid == uid))
                {
                    data.history_uid = uid;

                    sqlDb.historys.Add(new SisiHistorySqlModel
                    {
                        user = md5user,
                        uid = uid,
                        created = DateTime.UtcNow,
                        json = JsonConvert.SerializeObject(data)
                    });

                    await sqlDb.SaveChangesLocks();
                }
            }

            return Json(new
            {
                result = true
            });
        }


        [Route("sisi/history/remove")]
        async public Task<ActionResult> Remove(string id)
        {
            string md5user = getuser();
            if (md5user == null || !AppInit.conf.sisi.history.enable || string.IsNullOrEmpty(id))
                return OnError("access denied");

            try
            {
                await SisiContext.semaphore.WaitAsync(TimeSpan.FromSeconds(30));

                using (var sqlDb = new SisiContext())
                {
                    sqlDb.historys
                        .Where(i => i.user == md5user && i.uid == id)
                        .ExecuteDelete();
                }
            }
            catch { }
            finally
            {
                SisiContext.semaphore.Release();
            }

            return Json(new
            {
                result = true,
            });
        }



        string getuser()
        {
            if (!string.IsNullOrEmpty(requestInfo.user_uid))
                return CrypTo.md5(requestInfo.user_uid);

            return null;
        }
    }
}
