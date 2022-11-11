using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using System.IO;
using System.Web;
using System.Collections.Generic;
using Lampac.Models.DLNA;
using IO = System.IO;
using MonoTorrent.Client;
using System.Threading.Tasks;
using System.Linq;
using Lampac.Engine.CORE;
using MonoTorrent;
using Lampac.Engine.Parse;
using Microsoft.Extensions.Caching.Memory;

namespace Lampac.Controllers.PLUGINS
{
    public class DLNAController : BaseController
    {
        #region DLNAController
        static ClientEngine torrentEngine = new ClientEngine();

        static List<TorrentManager> torrentManagers = new List<TorrentManager>();
        #endregion

        [HttpGet]
        [Route("dlna.js")]
        public ActionResult Plugin()
        {
            string file = IO.File.ReadAllText("dlna.js");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }


        [HttpGet]
        [Route("dlna")]
        public JsonResult Index(string path)
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            var playlist = new List<DlnaModel>();

            foreach (string folder in Directory.GetDirectories("dlna/" + path))
            {
                playlist.Add(new DlnaModel() 
                {
                    type = "folder",
                    name = Path.GetFileName(folder),
                    uri = $"{AppInit.Host(HttpContext)}/dlna?path={HttpUtility.UrlEncode(folder.Replace("dlna/", ""))}",
                    path = folder.Replace("dlna/", ""),
                    length = Directory.GetFiles(folder).Length
                });
            }

            foreach (string file in Directory.GetFiles("dlna/" + path))
            {
                playlist.Add(new DlnaModel()
                {
                    type = "file",
                    name = Path.GetFileName(file),
                    uri = $"{AppInit.Host(HttpContext)}/dlna/stream?path={HttpUtility.UrlEncode(file.Replace("dlna/", ""))}",
                    path = file.Replace("dlna/", ""),
                    length = new FileInfo(file).Length
                });
            }

            return Json(playlist);
        }


        [Route("dlna/stream")]
        public ActionResult Stream(string path)
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            return File(IO.File.OpenRead("dlna/" + path), "application/octet-stream", true);
        }


        [HttpGet]
        [Route("dlna/delete")]
        public ActionResult Delete(string path)
        {
            if (!AppInit.conf.dlna)
                return Content(string.Empty);

            try
            {
                IO.File.Delete("dlna/" + path);
            }
            catch { }

            try
            {
                Directory.Delete("dlna/" + path, true);
            }
            catch { }

            return Content(string.Empty);
        }


        [HttpGet]
        [Route("dlna/tracker/managers")]
        public JsonResult Managers()
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            return Json(torrentManagers.Select(i => new
            {
                InfoHash = i.InfoHash.ToHex(),
                Engine = new 
                {
                    i.Engine.ConnectionManager.HalfOpenConnections,
                    i.Engine.ConnectionManager.OpenConnections,
                    i.Engine.TotalDownloadSpeed,
                    i.Engine.TotalUploadSpeed,
                },
                Files = i.Files.Select(f => new 
                {
                    f.Path,
                    f.FullPath,
                    f.Priority,
                    f.Length
                }),
                i.MagnetLink,
                i.MetadataPath,
                i.Monitor,
                i.OpenConnections,
                i.PartialProgress,
                i.Progress,
                i.Peers,
                i.PieceLength,
                i.SavePath,
                i.Size,
                i.State,
                i.Torrent,
                i.UploadingTo
            }));
        }


        [HttpGet]
        [Route("dlna/tracker/download")]
        async public Task<JsonResult> Download(string path)
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            string memKey = $"dlna:tracker:Download:{path}";
            if (memoryCache.TryGetValue(memKey, out _))
                return Json(new { status = true });

            try
            {
                string magnet = path;
                if (magnet.StartsWith("http"))
                {
                    var _t = await HttpClient.Download(path, timeoutSeconds: 10);
                    if (_t == null)
                        return Json(new { });

                    magnet = BencodeTo.Magnet(_t);
                    if (magnet == null)
                        return Json(new { });
                }

                memoryCache.Set(memKey, 0);

                var manager = await torrentEngine.AddStreamingAsync(MagnetLink.Parse(magnet), "dlna/");

                await manager.StartAsync();
                await manager.WaitForMetadataAsync();

                manager.TorrentStateChanged += async (s, e) =>
                {
                    if (e != null && e.NewState == TorrentState.Seeding)
                        await e.TorrentManager.StopAsync();

                    if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                    {
                        torrentManagers.Remove(manager);
                        memoryCache.Remove(memKey);
                    }
                };

                torrentManagers.Add(manager);
            }
            catch
            {
                memoryCache.Remove(memKey);
                return Json(new { });
            }

            return Json(new { status = true });
        }


        [HttpGet]
        [Route("dlna/tracker/delete")]
        async public Task<JsonResult> TorrentDelete(string infohash)
        {
            if (!AppInit.conf.dlna)
                return Json(new { });

            var manager = torrentManagers.FirstOrDefault(i => i.InfoHash.ToHex() == infohash);
            if (manager != null)
                await manager.StopAsync();

            return Json(new { status = true });
        }
    }
}
