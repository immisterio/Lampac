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
using MonoTorrent;
using Lampac.Engine.Parse;
using Microsoft.Extensions.Caching.Memory;
using System;

namespace Lampac.Controllers.PLUGINS
{
    public class DLNAController : BaseController
    {
        #region DLNAController
        static ClientEngine torrentEngine = new ClientEngine();

        static DLNAController()
        {
            if (!Directory.Exists("cache/metadata"))
                return;

            foreach (string path in Directory.GetFiles("cache/metadata", "*.torrent"))
            {
                var t = Torrent.Load(path);
                var manager = torrentEngine.AddAsync(t, "dlna/").Result;

                manager.TorrentStateChanged += async (s, e) =>
                {
                    if (e != null && e.NewState == TorrentState.Seeding)
                        await e.TorrentManager.StopAsync();

                    if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                        IO.File.Delete(path);
                };
            }

            torrentEngine.StartAllAsync();
        }
        #endregion

        [HttpGet]
        [Route("dlna.js")]
        public ActionResult Plugin()
        {
            string file = IO.File.ReadAllText("plugins/dlna.js");
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

            return Json(torrentEngine.Torrents.Where(i => i.State != TorrentState.Stopped).Select(i => new
            {
                InfoHash = i.InfoHash.ToHex(),
                //Engine = new 
                //{
                //    i.Engine.ConnectionManager.HalfOpenConnections,
                //    i.Engine.ConnectionManager.OpenConnections,
                //    i.Engine.TotalDownloadSpeed,
                //    i.Engine.TotalUploadSpeed,
                //},
                //Files = i.Files.Select(f => new 
                //{
                //    f.Path,
                //    f.FullPath,
                //    f.Priority,
                //    f.Length
                //}),
                //i.MagnetLink,
                //i.MetadataPath,
                i.Monitor,
                i.OpenConnections,
                i.PartialProgress,
                i.Progress,
                i.Peers,
                i.PieceLength,
                i.SavePath,
                i.Size,
                State = i.State.ToString(),
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
                memoryCache.Set(memKey, 0);

                #region Download
                if (magnet.StartsWith("http"))
                {
                    var handler = new System.Net.Http.HttpClientHandler()
                    {
                        AllowAutoRedirect = false
                    };

                    handler.ServerCertificateCustomValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;

                    using (var client = new System.Net.Http.HttpClient(handler))
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);

                        using (var response = await client.GetAsync(path))
                        {
                            if (response.StatusCode == System.Net.HttpStatusCode.OK)
                            {
                                using (var content = response.Content)
                                    magnet = BencodeTo.Magnet(await content.ReadAsByteArrayAsync());
                            }
                            else if (((int)response.StatusCode) is 301 or 302 or 307)
                            {
                                string location = response.Headers.Location?.ToString() ?? response.RequestMessage.RequestUri?.ToString();
                                if (location != null && location.StartsWith("magnet:"))
                                    magnet = location;
                            }
                        }
                    }

                    if (magnet == null)
                    {
                        memoryCache.Remove(memKey);
                        return Json(new { });
                    }
                }
                #endregion

                // https://github.com/ngosang/trackerslist
                string trackers = await IO.File.ReadAllTextAsync("trackers.txt");
                var manager = await torrentEngine.AddStreamingAsync(MagnetLink.Parse(magnet + trackers), "dlna/");

                await manager.StartAsync();
                await manager.WaitForMetadataAsync();

                manager.TorrentStateChanged += async (s, e) =>
                {
                    if (e != null && e.NewState == TorrentState.Seeding)
                        await e.TorrentManager.StopAsync();

                    if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                    {
                        memoryCache.Remove(memKey);
                        IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHash.ToHex()}.torrent");
                    }
                };

                if (manager.Files != null && manager.Files.Count > 1)
                    await manager.SetFilePriorityAsync(manager.Files[0], Priority.High);
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

            var manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHash.ToHex() == infohash);
            if (manager != null)
                await manager.StopAsync();

            return Json(new { status = true });
        }
    }
}
