using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Shared.Models.SQL;
using System.Web;

namespace SISI
{
    public class BookmarkController : BaseSisiController
    {
        [Route("sisi/bookmarks")]
        public ActionResult List(string search, string model, int pg = 1, int pageSize = 36)
        {
            string md5user = getuser();
            if (md5user == null)
                return OnError("access denied");

            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = $"{host}/sisi/bookmarks",
                }
            };

            #region bookmarks
            var bookmarks = new List<PlaylistItem>();
            var bookmarksQuery = new List<SisiBookmarkSqlModel>();

            using (var sqlDb = new SisiContext())
            {
                bookmarksQuery = sqlDb.bookmarks
                    .AsNoTracking()
                    .Where(i => i.user == md5user)
                    .ToList();
            }

            int total_pages = Math.Max(0, bookmarksQuery.Count / pageSize) + 1;

            #region Модель
            var menu_models = new MenuItem()
            {
                title = $"Модель: {model ?? "выбрать"}",
                playlist_url = "submenu",
                submenu = new List<MenuItem>(20)
            };

            foreach (var m in bookmarksQuery.OrderByDescending(i => i.created).Select(i => i.model).ToHashSet())
            {
                if (string.IsNullOrEmpty(m))
                    continue;

                menu_models.submenu.Add(new MenuItem()
                {
                    title = m,
                    playlist_url = $"{host}/sisi/bookmarks?model={HttpUtility.UrlEncode(m)}"
                });
            }

            if (menu_models.submenu.Count > 0)
                menu.Add(menu_models);
            #endregion

            var items = bookmarksQuery
                .OrderByDescending(i => i.created)
                .Skip((pg * pageSize) - pageSize)
                .Take(pageSize);

            if (!string.IsNullOrEmpty(search))
            {
                string _s = StringConvert.SearchName(search);
                items = items.Where(i => i.name != null && StringConvert.SearchName(i.name).Contains(_s));
            }

            if (!string.IsNullOrEmpty(model))
                items = items.Where(i => i.model == model);

            if (items.Any())
            {
                foreach (var json in items.Select(i => i.json))
                {
                    if (string.IsNullOrEmpty(json))
                        continue;

                    try
                    {
                        var bookmark = JsonConvert.DeserializeObject<PlaylistItem>(json);
                        if (bookmark != null)
                            bookmarks.Add(bookmark);
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
                menu,
                list = bookmarks.Select(pl => new
                {
                    pl.name,
                    video = getvideLink(pl),
                    picture = HostImgProxy(pl.bookmark.image.StartsWith("bookmarks/") ? $"{localhost}/{pl.bookmark.image}" : pl.bookmark.image, plugin: pl.bookmark.site),
                    pl.time,
                    pl.json,
                    related = pl.related || Regex.IsMatch(pl.bookmark.site, "^(elo|epr|fph|phub|sbg|xmr|xnx|xds)"),
                    pl.quality,
                    preview = pl.preview != null && pl.preview.StartsWith("bookmarks/") ? $"{host}/{pl.preview}" : null,
                    pl.model,
                    bookmark = new Bookmark() { uid = pl.bookmark.uid }
                }).ToArray(),
                total_pages
            });
        }


        [HttpPost]
        [Route("sisi/bookmark/add")]
        public async Task<ActionResult> Add([FromBody] PlaylistItem data)
        {
            string md5user = getuser();
            if (md5user == null || data == null || string.IsNullOrEmpty(data?.bookmark?.site) || string.IsNullOrEmpty(data?.bookmark?.href))
                return OnError("access denied");

            string uid = CrypTo.md5($"{data.bookmark.site}:{data.bookmark.href}");

            using (var sqlDb = new SisiContext())
            {
                if (!sqlDb.bookmarks.AsNoTracking().Any(i => i.user == md5user && i.uid == uid))
                {
                    string newimage = null;

                    #region download image
                    if (AppInit.conf.sisi.bookmarks.saveimage)
                    {
                        string pimg = $"bookmarks/img/{uid.Substring(0, 2)}/{uid.Substring(2)}.jpg";

                        if (System.IO.File.Exists($"wwwroot/{pimg}"))
                        {
                            newimage = pimg;
                        }
                        else
                        {
                            var image = await Http.Download(data.bookmark.image, timeoutSeconds: 7);
                            if (image != null)
                            {
                                Directory.CreateDirectory($"wwwroot/bookmarks/img/{uid.Substring(0, 2)}");
                                System.IO.File.WriteAllBytes($"wwwroot/{pimg}", image);
                                newimage = pimg;
                            }
                        }
                    }
                    #endregion

                    #region download preview
                    if (AppInit.conf.sisi.bookmarks.savepreview)
                    {
                        if (data.preview != null)
                        {
                            string path = $"bookmarks/preview/{uid.Substring(0, 2)}/{uid.Substring(2)}.{(data.preview.Contains(".webm") ? "webm" : "mp4")}";

                            if (System.IO.File.Exists($"wwwroot/{path}"))
                            {
                                data.preview = path;
                            }
                            else
                            {
                                var preview = await Http.Download(data.preview, timeoutSeconds: 8);
                                if (preview != null)
                                {
                                    Directory.CreateDirectory($"wwwroot/bookmarks/preview/{uid.Substring(0, 2)}");
                                    System.IO.File.WriteAllBytes($"wwwroot/{path}", preview);
                                    data.preview = path;
                                }
                            }
                        }
                    }
                    #endregion

                    var b = data.bookmark;
                    data.bookmark = new Bookmark()
                    {
                        href = b.href,
                        image = newimage ?? b.image,
                        site = b.site,
                        uid = uid
                    };

                    sqlDb.bookmarks.Add(new SisiBookmarkSqlModel
                    {
                        user = md5user,
                        uid = uid,
                        created = DateTime.UtcNow,
                        json = JsonConvert.SerializeObject(data),
                        name = data.name,
                        model = data.model?.name
                    });

                    await sqlDb.SaveChangesLocks();
                }
            }

            return Json(new
            {
                result = true
            });
        }


        [Route("sisi/bookmark/remove")]
        async public Task<ActionResult> Remove(string id)
        {
            string md5user = getuser();
            if (md5user == null || string.IsNullOrEmpty(id))
                return OnError("access denied");

            try
            {
                await SisiContext.semaphore.WaitAsync(TimeSpan.FromSeconds(30));

                using (var sqlDb = new SisiContext())
                {
                    sqlDb.bookmarks
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
