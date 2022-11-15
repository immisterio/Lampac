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
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Lampac.Controllers.PLUGINS
{
    public class DLNAController : BaseController
    {
        #region DLNAController
        static ClientEngine torrentEngine;

        static DLNAController()
        {
            EngineSettingsBuilder engineSettingsBuilder = new EngineSettingsBuilder()
            {
                MaximumConnections = 30,
                MaximumHalfOpenConnections = 20,
                MaximumUploadSpeed = 125000, // 1Mbit/s
                MaximumDownloadSpeed = AppInit.conf.dlna.downloadSpeed
            };

            torrentEngine = new ClientEngine(engineSettingsBuilder.ToSettings());


            if (!Directory.Exists("cache/metadata"))
                return;

            foreach (string path in Directory.GetFiles("cache/metadata", "*.torrent"))
            {
                var t = Torrent.Load(path);
                var manager = AppInit.conf.dlna.mode == "stream" ? torrentEngine.AddStreamingAsync(t, "dlna/").Result : torrentEngine.AddAsync(t, "dlna/").Result;

                //if (FastResume.TryLoad($"cache/fastresume/{t.InfoHash.ToHex()}.fresume", out FastResume resume))
                //    manager.LoadFastResume(resume);

                int[] indexs = null;

                try
                {
                    if (IO.File.Exists($"cache/metadata/{t.InfoHash.ToHex()}.json"))
                        indexs = JsonConvert.DeserializeObject<int[]>(IO.File.ReadAllText($"cache/metadata/{t.InfoHash.ToHex()}.json"));
                }
                catch { }

                bool setPriority = false;

                manager.TorrentStateChanged += async (s, e) =>
                {
                    if (e != null && e.NewState == TorrentState.Seeding)
                        await e.TorrentManager.StopAsync();

                    if (e != null && (e.NewState == TorrentState.Metadata || e.NewState == TorrentState.Hashing || e.NewState == TorrentState.Downloading))
                    {
                        if (!setPriority)
                        {
                            setPriority = true;

                            if (indexs == null || indexs.Length == 0)
                            {
                                await manager.SetFilePriorityAsync(manager.Files[0], Priority.High);
                            }
                            else
                            {
                                for (int i = 0; i < manager.Files.Count; i++)
                                {
                                    if (indexs.Contains(i))
                                    {
                                        await manager.SetFilePriorityAsync(manager.Files[i], i == indexs[0] ? Priority.High : Priority.Normal);
                                    }
                                    else
                                    {
                                        await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
                                    }
                                }
                            }
                        }
                    }

                    if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                    {
                        try
                        {
                            IO.File.Delete(path);
                            IO.File.Delete(path.Replace(".torrent", ".json"));
                        }
                        catch { }

                        foreach (var f in e.TorrentManager.Files)
                        {
                            try
                            {
                                if (f.Priority == Priority.DoNotDownload && IO.File.Exists(f.FullPath))
                                    IO.File.Delete(f.FullPath);
                            }
                            catch { }
                        }
                    }
                };
            }

            torrentEngine.StartAllAsync();
        }
        #endregion

        #region getMagnet
        async ValueTask<string> getMagnet(string path)
        {
            string trackers = await IO.File.ReadAllTextAsync("trackers.txt");

            if (!path.StartsWith("http"))
                return path + trackers;

            string memkey = $"DLNAController:getMagnet:{path}";
            if (!memoryCache.TryGetValue(memkey, out string magnet))
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
                    return null;

                memoryCache.Set(memkey, magnet, DateTime.Now.AddMinutes(10));
            }

            return magnet + trackers;
        }
        #endregion


        #region Plugin
        [HttpGet]
        [Route("dlna.js")]
        public ActionResult Plugin()
        {
            string file = IO.File.ReadAllText("plugins/dlna.js");
            file = file.Replace("{localhost}", AppInit.Host(HttpContext));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region Index
        [HttpGet]
        [Route("dlna")]
        public JsonResult Index(string path)
        {
            if (!AppInit.conf.dlna.enable)
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
        #endregion

        #region Stream
        [Route("dlna/stream")]
        public ActionResult Stream(string path)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            return File(IO.File.OpenRead("dlna/" + path), "application/octet-stream", true);
        }
        #endregion

        #region Delete
        [HttpGet]
        [Route("dlna/delete")]
        public ActionResult Delete(string path)
        {
            if (!AppInit.conf.dlna.enable)
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
        #endregion


        #region Managers
        [HttpGet]
        [Route("dlna/tracker/managers")]
        public JsonResult Managers()
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            return Json(torrentEngine.Torrents.Where(i => i.State != TorrentState.Stopped).Select(i => new
            {
                InfoHash = i.InfoHash.ToHex(),
                i.Torrent.Name,
                //Engine = new 
                //{
                //    i.Engine.ConnectionManager.HalfOpenConnections,
                //    i.Engine.ConnectionManager.OpenConnections,
                //    i.Engine.TotalDownloadSpeed,
                //    i.Engine.TotalUploadSpeed,
                //},
                Files = i.Files.Select(f => new
                {
                    f.Path,
                    f.Priority,
                    f.Length
                }),
                //i.MagnetLink,
                //i.MetadataPath,
                i.Monitor,
                i.OpenConnections,
                i.PartialProgress,
                i.Progress,
                i.Peers,
                //i.PieceLength,
                //i.SavePath,
                i.Size,
                State = i.State.ToString(),
                //i.Torrent,
                i.UploadingTo
            }));
        }
        #endregion

        #region Show
        [HttpGet]
        [Route("dlna/tracker/show")]
        async public Task<JsonResult> Show(string path)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            try
            {
                string magnet = await getMagnet(path);
                if (magnet == null)
                    return Json(new { });

                string hash = Regex.Match(magnet, "btih:([a-z0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLower();
                if (IO.File.Exists($"cache/torrent/{hash}"))
                    return Json(Torrent.Load(IO.File.ReadAllBytes($"cache/torrent/{hash}")).Files);

                byte[] data = await torrentEngine.DownloadMetadataAsync(MagnetLink.Parse(magnet), new System.Threading.CancellationToken());
                if (data == null)
                    return Json(new { });

                await IO.File.WriteAllBytesAsync($"cache/torrent/{hash}", data);

                return Json(Torrent.Load(data).Files);
            }
            catch
            {
                return Json(new { });
            }
        }
        #endregion

        #region Download
        [HttpGet]
        [Route("dlna/tracker/download")]
        async public Task<JsonResult> Download(string path, int[] indexs)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            try
            {
                string magnet = await getMagnet(path);
                if (magnet == null)
                    return Json(new { });

                // cache metadata
                string hash = Regex.Match(magnet, "btih:([a-z0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLower();
                if (IO.File.Exists($"cache/torrent/{hash}") && !IO.File.Exists($"cache/metadata/{hash.ToUpper()}.torrent"))
                    IO.File.Copy($"cache/torrent/{hash}", $"cache/metadata/{hash.ToUpper()}.torrent");

                var magnetLink = MagnetLink.Parse(magnet);
                TorrentManager manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHash.ToHex() == magnetLink.InfoHash.ToHex());

                if (manager != null)
                {
                    if (manager.State is TorrentState.Stopped or TorrentState.Stopping)
                    {
                        await manager.StartAsync();
                        await manager.WaitForMetadataAsync();
                    }
                }
                else
                {
                    manager = AppInit.conf.dlna.mode == "stream" ? await torrentEngine.AddStreamingAsync(MagnetLink.Parse(magnet), "dlna/") : await torrentEngine.AddAsync(MagnetLink.Parse(magnet), "dlna/");

                    await manager.StartAsync();
                    await manager.WaitForMetadataAsync();

                    manager.TorrentStateChanged += async (s, e) =>
                    {
                        if (e != null && e.NewState == TorrentState.Seeding)
                            await e.TorrentManager.StopAsync();

                        if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                        {
                            try
                            {
                                IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHash.ToHex()}.torrent");
                                IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHash.ToHex()}.json");
                            }
                            catch { }

                            foreach (var f in e.TorrentManager.Files)
                            {
                                try
                                {
                                    if (f.Priority == Priority.DoNotDownload && IO.File.Exists(f.FullPath))
                                        IO.File.Delete(f.FullPath);
                                }
                                catch { }
                            }
                        }
                    };
                }

                if (indexs == null || indexs.Length == 0)
                {
                    await manager.SetFilePriorityAsync(manager.Files[0], Priority.High);
                }
                else
                {
                    await IO.File.WriteAllTextAsync($"cache/metadata/{manager.InfoHash.ToHex()}.json", JsonConvert.SerializeObject(indexs));

                    for (int i = 0; i < manager.Files.Count; i++)
                    {
                        if (indexs.Contains(i))
                        {
                            await manager.SetFilePriorityAsync(manager.Files[i], i == indexs[0] ? Priority.High : Priority.Normal);
                        }
                        else
                        {
                            await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
                        }
                    }
                }
            }
            catch
            {
                return Json(new { });
            }

            return Json(new { status = true });
        }
        #endregion

        #region Delete
        [HttpGet]
        [Route("dlna/tracker/delete")]
        async public Task<JsonResult> TorrentDelete(string infohash)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            var manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHash.ToHex() == infohash);
            if (manager != null)
                await manager.StopAsync();

            return Json(new { status = true });
        }
        #endregion
    }
}
