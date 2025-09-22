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
        public async Task<ActionResult> List(string search, string model, int pg = 1, int pageSize = 36)
        {
            string md5user = getuser();
            if (md5user == null)
                return OnError("access denied");

            #region bookmarks
            var bookmarks = Enumerable.Empty<PlaylistItem>();

            using (var db = new SisiContext())
            {
                var items = await db.bookmarks
                    .AsNoTracking()
                    .Where(i => i.user == md5user)
                    .OrderByDescending(i => i.created)
                    .Select(i => i.json)
                    .ToListAsync();

                if (items.Count > 0)
                {
                    var list = new List<PlaylistItem>(items.Count);

                    foreach (var json in items)
                    {
                        if (string.IsNullOrEmpty(json))
                            continue;

                        try
                        {
                            var bookmark = JsonConvert.DeserializeObject<PlaylistItem>(json);
                            if (bookmark != null)
                                list.Add(bookmark);
                        }
                        catch { }
                    }

                    bookmarks = list;
                }
            }
            #endregion

            #region menu
            var menu = new List<MenuItem>()
            {
                new MenuItem()
                {
                    title = "Поиск",
                    search_on = "search_on",
                    playlist_url = $"{host}/sisi/bookmarks",
                }
            };

            var menu_models = new MenuItem()
            {
                title = $"Модель: {model ?? "выбрать"}",
                playlist_url = "submenu",
                submenu = new List<MenuItem>(20)
            };

            var temp_models = new HashSet<string>();
            foreach (var b in bookmarks)
            {
                if (string.IsNullOrEmpty(b.model?.name) || temp_models.Contains(b.model.name))
                    continue;

                temp_models.Add(b.model.name);

                menu_models.submenu.Add(new MenuItem()
                {
                    title = b.model.name,
                    playlist_url = $"{host}/sisi/bookmarks?model={HttpUtility.UrlEncode(b.model.name)}"
                });
            }

            if (menu_models.submenu.Count > 0)
                menu.Add(menu_models);
            #endregion

            if (!string.IsNullOrEmpty(search))
            {
                string _s = search.ToLower();
                bookmarks = bookmarks.Where(i => i.name.ToLower().Contains(_s));
            }

            if (!string.IsNullOrEmpty(model))
                bookmarks = bookmarks.Where(i => i.model?.name == model);

            string getvideLink(PlaylistItem pl)
            {
                if (pl.bookmark.site is "phub" or "phubprem")
                    return $"{host}/{pl.bookmark.site}/vidosik?vkey={HttpUtility.UrlEncode(pl.bookmark.href)}";

                return $"{host}/{pl.bookmark.site}/vidosik?uri={HttpUtility.UrlEncode(pl.bookmark.href)}";
            }

            string localhost = $"http://{AppInit.conf.listen.localhost}:{AppInit.conf.listen.port}";

            return new JsonResult(new
            {
                menu,
                list = bookmarks.Skip((pg * pageSize) - pageSize).Take(pageSize).Select(pl => new
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
                }).ToArray()
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

            using (var db = new SisiContext())
            {
                if (!await db.bookmarks.AsNoTracking().AnyAsync(i => i.user == md5user && i.uid == uid))
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

                    db.bookmarks.Add(new SisiBookmarkSqlModel
                    {
                        user = md5user,
                        uid = uid,
                        created = DateTime.UtcNow,
                        json = JsonConvert.SerializeObject(data)
                    });

                    await db.SaveChangesAsync();
                }
            }

            return Json(new
            {
                result = true
            });
        }


        [Route("sisi/bookmark/remove")]
        public ActionResult Remove(string id)
        {
            string md5user = getuser();
            if (md5user == null || string.IsNullOrEmpty(id))
                return OnError("access denied");

            using (var db = new SisiContext())
            {
                var bookmark = db.bookmarks.FirstOrDefault(i => i.user == md5user && i.uid == id);
                if (bookmark != null)
                {
                    db.bookmarks.Remove(bookmark);
                    db.SaveChanges();
                }
            }

            return Json(new
            {
                result = false,
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
