using Lampac;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Model.SISI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace SISI
{
    public class BookmarkController : BaseSisiController
    {
        static BookmarkController() 
        {
            Directory.CreateDirectory("wwwroot/bookmarks/img");
            Directory.CreateDirectory("wwwroot/bookmarks/preview");
        }

        [Route("sisi/bookmarks")]
        public ActionResult List(string search, string model, int pg = 1, int pageSize = 36)
        {
            string md5user = getuser();
            if (md5user == null)
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);
            var bookmarks = bookmarkCache.Read().AsEnumerable();

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
                if (string.IsNullOrEmpty(b.model?.name) || temp_models.Contains(b.model.Value.name))
                    continue;

                temp_models.Add(b.model.Value.name);

                menu_models.submenu.Add(new MenuItem() 
                {
                    title = b.model.Value.name,
                    playlist_url = $"{host}/sisi/bookmarks?model={HttpUtility.UrlEncode(b.model.Value.name)}"
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
                if (pl.bookmark.Value.site is "phub" or "phubprem")
                    return $"{host}/{pl.bookmark.Value.site}/vidosik?vkey={HttpUtility.UrlEncode(pl.bookmark.Value.href)}";

                return $"{host}/{pl.bookmark.Value.site}/vidosik?uri={HttpUtility.UrlEncode(pl.bookmark.Value.href)}";
            }

            string localhost = $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}";

            return new JsonResult(new
            {
                menu,
                list = bookmarks.Skip((pg * pageSize) - pageSize).Take(pageSize).Select(pl => new
                {
                    pl.name,
                    video = getvideLink(pl),
                    picture = HostImgProxy(pl.bookmark.Value.image.StartsWith("bookmarks/") ? $"{localhost}/{pl.bookmark.Value.image}" : pl.bookmark.Value.image, plugin: pl.bookmark.Value.site),
                    pl.time,
                    pl.json,
                    related = pl.related || Regex.IsMatch(pl.bookmark.Value.site, "^(elo|epr|fph|phub|sbg|xmr|xnx|xds)"),
                    pl.quality,
                    preview = pl.preview != null && pl.preview.StartsWith("bookmarks/") ? $"{host}/{pl.preview}" : null,
                    pl.model,
                    bookmark = new Bookmark() { uid = pl.bookmark.Value.uid }
                })
            });
        }


        [HttpPost]
        [Route("sisi/bookmark/add")]
        public async Task<ActionResult> Add([FromBody] PlaylistItem data)
        {
            string md5user = getuser();
            if (md5user == null || data == null || string.IsNullOrEmpty(data?.bookmark?.site) || string.IsNullOrEmpty(data?.bookmark?.href))
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);

            var bookmarks = bookmarkCache.Read();
            string uid = CrypTo.md5($"{data.bookmark.Value.site}:{data.bookmark.Value.href}");

            if (bookmarks.FirstOrDefault(i => i.bookmark.Value.uid == uid) == null)
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
                        var image = await HttpClient.Download(data.bookmark.Value.image, timeoutSeconds: 7);
                        if (image != null)
                        {
                            Directory.CreateDirectory($"wwwroot/bookmarks/img/{uid.Substring(0, 2)}");
                            await System.IO.File.WriteAllBytesAsync($"wwwroot/{pimg}", image);
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
                            var preview = await HttpClient.Download(data.preview, timeoutSeconds: 8);
                            if (preview != null)
                            {
                                Directory.CreateDirectory($"wwwroot/bookmarks/preview/{uid.Substring(0, 2)}");
                                await System.IO.File.WriteAllBytesAsync($"wwwroot/{path}", preview);
                                data.preview = path;
                            }
                        }
                    }
                }
                #endregion

                var b = data.bookmark;
                data.bookmark = new Bookmark()
                {
                    href = b.Value.href,
                    image = newimage ?? b.Value.image,
                    site = b.Value.site,
                    uid = uid
                };

                bookmarks.Insert(0, data);
                bookmarkCache.Write(bookmarks);
            }

            return Json(new
            {
                result = true,
            });
        }


        [Route("sisi/bookmark/remove")]
        public ActionResult Remove(string id)
        {
            string md5user = getuser();
            if (md5user == null || string.IsNullOrEmpty(id))
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);

            var bookmarks = bookmarkCache.Read();

            if (bookmarks.FirstOrDefault(i => i.bookmark.Value.uid == id) is PlaylistItem item)
            {
                bookmarks.Remove(item);
                bookmarkCache.Write(bookmarks);

                return Json(new
                {
                    result = true,
                });
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
