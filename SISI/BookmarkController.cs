using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using Lampac;
using Lampac.Engine.CORE;
using Lampac.Models.SISI;
using Microsoft.AspNetCore.Mvc;
using Shared.Engine.CORE;
using Shared.Model.SISI;

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
        public ActionResult List(string box_mac, string account_email, int pg = 1, int pageSize = 36)
        {
            string md5user = getuser(box_mac, account_email);
            if (md5user == null)
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);
            var bookmarks = bookmarkCache.Read();

            string getvideLink(PlaylistItem pl)
            {
                if (pl.bookmark.site is "phub" or "phubprem")
                    return $"{host}/{pl.bookmark.site}/vidosik?vkey={HttpUtility.UrlEncode(pl.bookmark.href)}";

                return $"{host}/{pl.bookmark.site}/vidosik?uri={HttpUtility.UrlEncode(pl.bookmark.href)}";
            }

            string localhost = $"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}";

            return new JsonResult(new
            {
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
                })
            });
        }


        [HttpPost]
        [Route("sisi/bookmark/add")]
        public async Task<ActionResult> Add([FromQuery] string box_mac, [FromQuery] string account_email, [FromBody] PlaylistItem data)
        {
            string md5user = getuser(box_mac, account_email);
            if (md5user == null || data == null || string.IsNullOrEmpty(data?.bookmark?.site) || string.IsNullOrEmpty(data?.bookmark?.href))
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);

            var bookmarks = bookmarkCache.Read();
            string uid = CrypTo.md5($"{data.bookmark.site}:{data.bookmark.href}");

            if (bookmarks.FirstOrDefault(i => i.bookmark.uid == uid) == null)
            {
                #region download image
                if (AppInit.conf.sisi.bookmarks.saveimage)
                {
                    string pimg = $"bookmarks/img/{uid.Substring(0, 2)}/{uid.Substring(2)}.jpg";

                    if (System.IO.File.Exists($"wwwroot/{pimg}"))
                    {
                        data.bookmark.image = pimg;
                    }
                    else
                    {
                        var image = await HttpClient.Download(data.bookmark.image, timeoutSeconds: 7);
                        if (image != null)
                        {
                            Directory.CreateDirectory($"wwwroot/bookmarks/img/{uid.Substring(0, 2)}");
                            System.IO.File.WriteAllBytes($"wwwroot/{pimg}", image);
                            data.bookmark.image = pimg;
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

                data.bookmark.uid = uid;
                bookmarks.Insert(0, data);
                bookmarkCache.Write(bookmarks);
            }

            return Json(new
            {
                result = true,
            });
        }


        [Route("sisi/bookmark/remove")]
        public ActionResult Remove(string box_mac, string account_email, string uid)
        {
            string md5user = getuser(box_mac, account_email);
            if (md5user == null || string.IsNullOrEmpty(uid))
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);

            var bookmarks = bookmarkCache.Read();

            if (bookmarks.FirstOrDefault(i => i.bookmark.uid == uid) is PlaylistItem item)
            {
                bookmarks.Remove(item);
                bookmarkCache.Write(bookmarks);
            }

            return Json(new
            {
                result = true,
            });
        }



        string getuser(string box_mac, string account_email)
        {
            if (!string.IsNullOrWhiteSpace(account_email))
                return CrypTo.md5(account_email);

            if (!string.IsNullOrWhiteSpace(box_mac) && box_mac.Length > 2)
                return CrypTo.md5(box_mac.ToLower().Trim());

            return null;
        }
    }
}
