using System.IO;
using System.Linq;
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
            Directory.CreateDirectory("cache/bookmarks/img");
            Directory.CreateDirectory("cache/bookmarks/preview");
        }

        [Route("sisi/bookmarks")]
        async public Task<ActionResult> List(string box_mac, string account_email, int pg = 1, int pageSize = 36)
        {
            string md5user = getuser(box_mac, account_email);
            if (md5user == null)
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);
            var bookmarks = await bookmarkCache.Read();

            string getvideLink(PlaylistItem pl)
            {
                if (pl.bookmark.site is "elo" or "epr" or "ptx" or "hqr" or "sbg")
                    return $"{host}/{pl.bookmark.site}/vidosik?uri={HttpUtility.UrlEncode(pl.bookmark.href)}";

                if (pl.bookmark.site is "phub" or "phubprem")
                    return $"{host}/{pl.bookmark.site}/vidosik.m3u8?vkey={HttpUtility.UrlEncode(pl.bookmark.href)}";

                return $"{host}/{pl.bookmark.site}/vidosik.m3u8?uri={HttpUtility.UrlEncode(pl.bookmark.href)}";
            }

            return new JsonResult(new
            {
                list = bookmarks.Skip((pg * pageSize) - pageSize).Take(pageSize).Select(pl => new
                {
                    pl.name,
                    video = getvideLink(pl),
                    picture = HostImgProxy(0, AppInit.conf.sisi.heightPicture, pl.bookmark.image.StartsWith("cache/") ? $"{host}/{pl.bookmark.image}" : pl.bookmark.image),
                    pl.time,
                    pl.json,
                    pl.quality,
                    preview = pl.preview != null && pl.preview.StartsWith("cache/") ? $"{host}/{pl.preview}" : null,
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

            var bookmarks = await bookmarkCache.Read();
            string uid = CrypTo.md5($"{data.bookmark.site}:{data.bookmark.href}");

            if (bookmarks.FirstOrDefault(i => i.bookmark.uid == uid) == null)
            {
                #region download image
                string pimg = $"cache/bookmarks/img/{uid.Substring(0, 2)}/{uid.Substring(2)}.jpg";

                if (System.IO.File.Exists(pimg))
                {
                    data.bookmark.image = pimg;
                }
                else
                {
                    var image = await HttpClient.Download(data.bookmark.image, timeoutSeconds: 5);
                    if (image != null)
                    {
                        Directory.CreateDirectory($"cache/bookmarks/img/{uid.Substring(0, 2)}");
                        await System.IO.File.WriteAllBytesAsync(pimg, image);
                        data.bookmark.image = pimg;
                    }
                }
                #endregion

                #region download preview
                if (data.preview != null)
                {
                    string path = $"cache/bookmarks/preview/{uid.Substring(0, 2)}/{uid.Substring(2)}.{(data.preview.Contains(".webm") ? "webm" : "mp4")}";

                    if (System.IO.File.Exists(path))
                    {
                        data.preview = path;
                    }
                    else
                    {
                        var preview = await HttpClient.Download(data.preview, timeoutSeconds: 8);
                        if (preview != null)
                        {
                            Directory.CreateDirectory($"cache/bookmarks/preview/{uid.Substring(0, 2)}");
                            await System.IO.File.WriteAllBytesAsync(path, preview);
                            data.preview = path;
                        }
                    }
                }
                #endregion

                data.bookmark.uid = uid;
                bookmarks.Insert(0, data);
                await bookmarkCache.Write(bookmarks);
            }

            return Json(new
            {
                result = true,
            });
        }


        [Route("sisi/bookmark/remove")]
        async public Task<ActionResult> Remove(string box_mac, string account_email, string uid)
        {
            string md5user = getuser(box_mac, account_email);
            if (md5user == null || string.IsNullOrEmpty(uid))
                return OnError("access denied");

            var bookmarkCache = new BookmarkCache<PlaylistItem>("sisi", md5user);

            var bookmarks = await bookmarkCache.Read();

            if (bookmarks.FirstOrDefault(i => i.bookmark.uid == uid) is PlaylistItem item)
            {
                bookmarks.Remove(item);
                await bookmarkCache.Write(bookmarks);
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
