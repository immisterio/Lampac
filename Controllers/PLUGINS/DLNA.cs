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
using Lampac.Engine.CORE;
using System.Threading;

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
            torrentEngine.DhtEngine.StartAsync();


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

        #region getTorrent
        async ValueTask<(string magnet, byte[] torrent)> getTorrent(string path)
        {
            if (!path.StartsWith("http"))
                return (path, null);

            string memkey = $"DLNAController:getTorrent:{path}";
            if (!memoryCache.TryGetValue(memkey, out (string magnet, byte[] torrent) cache))
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
                            {
                                var t = await content.ReadAsByteArrayAsync();
                                cache.magnet = BencodeTo.Magnet(t);
                                if (cache.magnet != null)
                                    cache.torrent = t;
                            }
                        }
                        else if (((int)response.StatusCode) is 301 or 302 or 307)
                        {
                            string location = response.Headers.Location?.ToString() ?? response.RequestMessage.RequestUri?.ToString();
                            if (location != null && location.StartsWith("magnet:"))
                                cache.magnet = location;
                        }
                    }
                }

                if (cache.magnet == null && cache.torrent == null)
                    return (null, null);

                memoryCache.Set(memkey, cache, DateTime.Now.AddMinutes(10));
            }

            return (cache.magnet, cache.torrent);
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

        #region Navigation
        [HttpGet]
        [Route("dlna")]
        public JsonResult Index(string path)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            #region getImage
            string getImage(string name)
            {
                string pathimage = $"thumbs/{CrypTo.md5(name)}.jpg";
                if (IO.File.Exists("dlna/" + pathimage))
                    return $"{AppInit.Host(HttpContext)}/dlna/stream?path={HttpUtility.UrlEncode(pathimage)}";

                return null;
            }
            #endregion

            var playlist = new List<DlnaModel>();

            foreach (string folder in Directory.GetDirectories("dlna/" + path))
            {
                if (folder.Contains("thumbs"))
                    continue;

                playlist.Add(new DlnaModel() 
                {
                    type = "folder",
                    name = Path.GetFileName(folder),
                    uri = $"{AppInit.Host(HttpContext)}/dlna?path={HttpUtility.UrlEncode(folder.Replace("dlna/", ""))}",
                    img = getImage(Path.GetFileName(folder)),
                    path = folder.Replace("dlna/", ""),
                    length = Directory.GetFiles(folder).Length,
                    creationTime = Directory.GetCreationTime(folder)
                });
            }

            foreach (string file in Directory.GetFiles("dlna/" + path))
            {
                if (!Regex.IsMatch(Path.GetExtension(file), "(aac|flac|mpga|mpega|mp2|mp3|m4a|oga|ogg|opus|spx|opus|weba|wav|dif|dv|fli|mp4|mpeg|mpg|mpe|mpv|mkv|ts|m2ts|mts|ogv|webm|avi|qt|mov)"))
                    continue;

                string name = Path.GetFileName(file);
                var fileinfo = new FileInfo(file);

                var dlnaModel = new DlnaModel()
                {
                    type = "file",
                    name = name,
                    uri = $"{AppInit.Host(HttpContext)}/dlna/stream?path={HttpUtility.UrlEncode(file.Replace("dlna/", ""))}",
                    img = getImage(name),
                    subtitles = new List<Subtitle>(),
                    path = file.Replace("dlna/", ""),
                    length = fileinfo.Length,
                    creationTime = fileinfo.CreationTime
                };

                if (IO.File.Exists($"dlna/{path}/{Path.GetFileNameWithoutExtension(file)}.srt"))
                {
                    dlnaModel.subtitles.Add(new Subtitle()
                    {
                        label = "Sub #1",
                        url = $"{AppInit.Host(HttpContext)}/dlna/stream?path={HttpUtility.UrlEncode($"{path}/{Path.GetFileNameWithoutExtension(file)}.srt")}"
                    });
                }

                playlist.Add(dlnaModel);
            }

            if (string.IsNullOrWhiteSpace(path))
                return Json(playlist.OrderByDescending(i => i.creationTime));

            return Json(playlist.OrderBy(i => 
            {
                ulong.TryParse(Regex.Replace(i.name, "[^0-9]+", ""), out ulong ident);
                return ident;
            }));
        }
        #endregion

        #region Stream
        [Route("dlna/stream")]
        public ActionResult Stream(string path)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            string contentType = "application/octet-stream";

            if (path.EndsWith(".jpg"))
                contentType = "image/jpeg";

            return File(IO.File.OpenRead("dlna/" + path), contentType, true);
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
                    f.Length,
                    BytesDownloaded = f.BytesDownloaded()
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
                var tparse = await getTorrent(path);

                if (tparse.torrent != null)
                    return Json(Torrent.Load(tparse.torrent).Files);

                if (tparse.magnet == null)
                    return Json(new { });

                string hash = Regex.Match(tparse.magnet, "btih:([a-z0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLower();
                if (IO.File.Exists($"cache/torrent/{hash}"))
                    return Json(Torrent.Load(IO.File.ReadAllBytes($"cache/torrent/{hash}")).Files);

                var s_cts = new CancellationTokenSource();
                s_cts.CancelAfter(1000 * 60 * 2);

                #region trackers
                string trackers = string.Empty;

                foreach (string line in IO.File.ReadLines("trackers.txt"))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (line.StartsWith("http") || line.StartsWith("udp:"))
                        trackers += $"&tr={line}";
                }
                #endregion

                byte[] data = await torrentEngine.DownloadMetadataAsync(MagnetLink.Parse(tparse.magnet + trackers), s_cts.Token);
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
        async public Task<JsonResult> Download(string path, int[] indexs, string thumb)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            try
            {
                var tparse = await getTorrent(path);
                if (tparse.magnet == null)
                    return Json(new { });

                // cache metadata
                string hash = Regex.Match(tparse.magnet, "btih:([a-z0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLower();
                if (IO.File.Exists($"cache/torrent/{hash}") && !IO.File.Exists($"cache/metadata/{hash.ToUpper()}.torrent"))
                    IO.File.Copy($"cache/torrent/{hash}", $"cache/metadata/{hash.ToUpper()}.torrent");

                var magnetLink = MagnetLink.Parse(tparse.magnet);
                TorrentManager manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHash.ToHex() == magnetLink.InfoHash.ToHex());

                if (manager != null)
                {
                    if (manager.State is TorrentState.Stopped or TorrentState.Stopping or TorrentState.Error)
                    {
                        await manager.StartAsync();
                        await manager.WaitForMetadataAsync();
                    }
                }
                else
                {
                    dynamic tlink = tparse.torrent != null ? Torrent.Load(tparse.torrent) : magnetLink;
                    manager = AppInit.conf.dlna.mode == "stream" ? await torrentEngine.AddStreamingAsync(tlink, "dlna/") : await torrentEngine.AddAsync(tlink, "dlna/");

                    #region AddTrackerAsync
                    foreach (string line in IO.File.ReadLines("trackers.txt"))
                    {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.StartsWith("http") || line.StartsWith("udp:"))
                            await manager.TrackerManager.AddTrackerAsync(new Uri(line));
                    }
                    #endregion

                    await manager.StartAsync();
                    await manager.WaitForMetadataAsync();

                    #region TorrentStateChanged
                    manager.TorrentStateChanged += async (s, e) =>
                    {
                        if (e != null && e.NewState == TorrentState.Seeding)
                            await e.TorrentManager.StopAsync();

                        //if (e != null && e.NewState == TorrentState.Error)
                        //    await e.TorrentManager.StartAsync();

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
                    #endregion
                }

                #region indexs
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
                            var priority = Priority.Normal;
                            if (i == indexs[0])
                                priority = Priority.Highest;
                            else if (indexs.Length > 1 && i == indexs[1])
                                priority = Priority.High;

                            await manager.SetFilePriorityAsync(manager.Files[i], priority);
                        }
                        else
                        {
                            await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
                        }
                    }
                }
                #endregion

                #region thumb
                if (thumb != null)
                {
                    try
                    {
                        var array = await HttpClient.Download(thumb);
                        if (array != null)
                        {
                            Directory.CreateDirectory("dlna/thumbs/");
                            await IO.File.WriteAllBytesAsync($"dlna/thumbs/{CrypTo.md5(manager.Torrent.Name)}.jpg", array);
                        }
                    }
                    catch { }
                }
                #endregion
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
