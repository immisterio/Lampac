﻿using Microsoft.AspNetCore.Mvc;
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
using Shared.Engine;
using MongoDB.Driver;

namespace Lampac.Controllers
{
    public class DLNAController : BaseController
    {
        #region DLNAController
        static string dlna_path => AppInit.conf.dlna.path;

        static string defTrackers = "tr=http://retracker.local/announce&tr=http%3A%2F%2Fbt4.t-ru.org%2Fann%3Fmagnet&tr=http://retracker.mgts.by:80/announce&tr=http://tracker.city9x.com:2710/announce&tr=http://tracker.electro-torrent.pl:80/announce&tr=http://tracker.internetwarriors.net:1337/announce&tr=http://tracker2.itzmx.com:6961/announce&tr=udp://opentor.org:2710&tr=udp://public.popcorn-tracker.org:6969/announce&tr=udp://tracker.opentrackr.org:1337/announce&tr=http://bt.svao-ix.ru/announce&tr=udp://explodie.org:6969/announce&tr=wss://tracker.btorrent.xyz&tr=wss://tracker.openwebtorrent.com";

        static ClientEngine torrentEngine;

        static DLNAController()
        {
            if (!Directory.Exists("cache/metadata"))
                return;

            string trackers_best_ip = HttpClient.Get("https://raw.githubusercontent.com/ngosang/trackerslist/master/trackers_best_ip.txt", timeoutSeconds: 5).Result;
            if (trackers_best_ip != null)
            {
                foreach (string line in trackers_best_ip.Split("\n"))
                {
                    string tr = line.Replace("\n", "").Replace("\r", "").Trim();
                    if (!string.IsNullOrWhiteSpace(tr))
                        defTrackers += $"&tr={tr}";
                }
            }

            foreach (string path in Directory.GetFiles("cache/metadata", "*.torrent"))
            {
                bullderClientEngine();

                var t = Torrent.Load(path);
                var manager = AppInit.conf.dlna.mode == "stream" ? torrentEngine.AddStreamingAsync(t, $"{dlna_path}/").Result : torrentEngine.AddAsync(t, $"{dlna_path}/").Result;

                //if (FastResume.TryLoad($"cache/fastresume/{t.InfoHash.ToHex()}.fresume", out FastResume resume))
                //    manager.LoadFastResume(resume);

                int[] indexs = null;

                try
                {
                    if (IO.File.Exists($"cache/metadata/{t.InfoHashes.V1.ToHex()}.json"))
                        indexs = JsonConvert.DeserializeObject<int[]>(IO.File.ReadAllText($"cache/metadata/{t.InfoHashes.V1.ToHex()}.json"));
                }
                catch { }

                bool setPriority = false;

                manager.TorrentStateChanged += async (s, e) =>
                {
                    try
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

                            await removeClientEngine(e.TorrentManager.InfoHashes.V1.ToHex().ToLower());
                        }
                    }
                    catch { }
                };
            }

            if (torrentEngine != null)
                torrentEngine.StartAllAsync();
        }
        #endregion

        #region dlna.js
        [HttpGet]
        [Route("dlna.js")]
        [Route("dlna/js/{token}")]
        public ActionResult Plugin(string token)
        {
            if (!AppInit.conf.dlna.enable)
                return Content(string.Empty);

            string file = FileCache.ReadAllText("plugins/dlna.js").Replace("{localhost}", host);
            file = file.Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region bullderClientEngine
        static Task bullderClientEngine(int connectionTimeout = 10)
        {
            if (torrentEngine != null)
                return Task.CompletedTask;

            EngineSettingsBuilder engineSettingsBuilder = new EngineSettingsBuilder()
            {
                MaximumHalfOpenConnections = 20,
                ConnectionTimeout = TimeSpan.FromSeconds(connectionTimeout),
                MaximumDownloadRate = AppInit.conf.dlna.downloadSpeed,
                MaximumUploadRate = AppInit.conf.dlna.uploadSpeed,
                MaximumDiskReadRate = AppInit.conf.dlna.maximumDiskReadRate,
                MaximumDiskWriteRate = AppInit.conf.dlna.maximumDiskWriteRate
            };

            torrentEngine = new ClientEngine(engineSettingsBuilder.ToSettings());
            return torrentEngine.StartAllAsync();
        }
        #endregion

        #region removeClientEngine
        async static Task removeClientEngine(string hash = null)
        {
            try
            {
                if (torrentEngine != null)
                {
                    var tdl = new List<TorrentManager>();

                    foreach (var i in torrentEngine.Torrents)
                    {
                        if (i.State == TorrentState.Stopped || i.State == TorrentState.Stopping || i.State == TorrentState.Error || i.InfoHashes.V1.ToHex().ToLower() == hash)
                        {
                            try
                            {
                                await i.StopAsync(TimeSpan.FromSeconds(20));
                            }
                            catch { }

                            tdl.Add(i);
                        }
                    }

                    if (tdl.Count > 0)
                    {
                        foreach (var item in tdl)
                        {
                            try
                            {
                                torrentEngine.Torrents.Remove(item);
                            }
                            catch { }
                        }

                    }

                    if (torrentEngine.Torrents.Count == 0)
                    {
                        try
                        {
                            await torrentEngine.StopAllAsync();
                        }
                        catch { }

                        torrentEngine.Dispose();
                        torrentEngine = null;
                    }
                }
            }
            catch { }
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
                        else if ((int)response.StatusCode is 301 or 302 or 307)
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
                if (IO.File.Exists($"{dlna_path}/" + pathimage))
                    return $"{host}/dlna/stream?path={HttpUtility.UrlEncode(pathimage)}";

                return null;
            }
            #endregion

            var playlist = new List<DlnaModel>();

            foreach (string folder in Directory.GetDirectories($"{dlna_path}/" + path))
            {
                if (folder.Contains("thumbs"))
                    continue;

                playlist.Add(new DlnaModel()
                {
                    type = "folder",
                    name = Path.GetFileName(folder),
                    uri = $"{host}/dlna?path={HttpUtility.UrlEncode(folder.Replace($"{dlna_path}/", ""))}",
                    img = getImage(Path.GetFileName(folder)),
                    path = folder.Replace($"{dlna_path}/", ""),
                    length = Directory.GetFiles(folder).Length,
                    creationTime = Directory.GetCreationTime(folder)
                });
            }

            foreach (string file in Directory.GetFiles($"{dlna_path}/" + path))
            {
                if (!Regex.IsMatch(Path.GetExtension(file), "(aac|flac|mpga|mpega|mp2|mp3|m4a|oga|ogg|opus|spx|opus|weba|wav|dif|dv|fli|mp4|mpeg|mpg|mpe|mpv|mkv|ts|m2ts|mts|ogv|webm|avi|qt|mov)"))
                    continue;

                string name = Path.GetFileName(file);
                var fileinfo = new FileInfo(file);

                var dlnaModel = new DlnaModel()
                {
                    type = "file",
                    name = name,
                    uri = $"{host}/dlna/stream?path={HttpUtility.UrlEncode(file.Replace($"{dlna_path}/", ""))}",
                    img = getImage(name),
                    subtitles = new List<Subtitle>(),
                    path = file.Replace($"{dlna_path}/", ""),
                    length = fileinfo.Length,
                    creationTime = fileinfo.CreationTime
                };

                if (IO.File.Exists($"{dlna_path}/{path}/{Path.GetFileNameWithoutExtension(file)}.srt"))
                {
                    dlnaModel.subtitles.Add(new Subtitle()
                    {
                        label = "Sub #1",
                        url = $"{host}/dlna/stream?path={HttpUtility.UrlEncode($"{path}/{Path.GetFileNameWithoutExtension(file)}.srt")}"
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

            return File(IO.File.OpenRead($"{dlna_path}/" + path), contentType, true);
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
                IO.File.Delete($"{dlna_path}/" + path);
            }
            catch { }

            try
            {
                Directory.Delete($"{dlna_path}/" + path, true);
            }
            catch { }

            return Content(string.Empty);
        }
        #endregion


        #region Managers
        [HttpGet]
        [Route("dlna/tracker/managers")]
        public ActionResult Managers()
        {
            if (!AppInit.conf.dlna.enable || torrentEngine == null)
                return Content("[]");

            return Json(torrentEngine.Torrents.Where(i => i.State != TorrentState.Stopped).Select(i => new
            {
                InfoHash = i.InfoHashes.V1.ToHex(),
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
                i.Monitor,
                i.OpenConnections,
                i.PartialProgress,
                i.Progress,
                i.Peers,
                State = i.State.ToString(),
                i.UploadingTo,
                i.Torrent.AnnounceUrls
            }));
        }
        #endregion

        #region Show
        [HttpGet]
        [Route("dlna/tracker/show")]
        async public Task<JsonResult> Show(string path)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { error = "enable" });

            try
            {
                var tparse = await getTorrent(path);
                if (tparse.torrent != null)
                    return Json(Torrent.Load(tparse.torrent).Files.Select(i => new { i.Path }));

                if (tparse.magnet == null)
                    return Json(new { error = "magnet" });

                string hash = Regex.Match(tparse.magnet, "btih:([a-z0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLower();
                if (IO.File.Exists($"cache/torrent/{hash}"))
                    return Json(Torrent.Load(IO.File.ReadAllBytes($"cache/torrent/{hash}")).Files.Select(i => new { i.Path }));

                var s_cts = new CancellationTokenSource();
                s_cts.CancelAfter(1000 * 60 * 2);

                string magnet = tparse.magnet;
                magnet += (magnet.Contains("?") ? "&" : "?") + defTrackers;

                #region trackers
                //if (IO.File.Exists("cache/trackers.txt") && AppInit.conf.dlna.addTrackersToMagnet)
                //{
                //    foreach (string line in IO.File.ReadLines("cache/trackers.txt"))
                //    {
                //        if (string.IsNullOrWhiteSpace(line))
                //            continue;

                //        if (line.StartsWith("http") || line.StartsWith("udp:"))
                //        {
                //            string host = line.Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();
                //            string tr = HttpUtility.UrlEncode(host);

                //            if (!magnet.Contains(tr))
                //                magnet += $"&tr={tr}";
                //        }
                //    }
                //}
                #endregion

                await bullderClientEngine(5);

                if (torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex().ToLower() == hash) is TorrentManager manager)
                {
                    await manager.WaitForMetadataAsync(s_cts.Token);
                    var files = manager.Files.Select(i => (ITorrentFile)i);
                    await removeClientEngine(hash);
                    return Json(files.Select(i => new { i.Path }));
                }

                var data = await torrentEngine.DownloadMetadataAsync(MagnetLink.Parse(magnet), s_cts.Token);
                if (data.IsEmpty)
                {
                    await removeClientEngine(hash);
                    return Json(new { error = "DownloadMetadata" });
                }

                var array = data.Span.ToArray();
                Directory.CreateDirectory("cache/torrent");
                IO.File.WriteAllBytes($"cache/torrent/{hash}", array);
                await removeClientEngine(hash);

                return Json(Torrent.Load(array).Files.Select(i => new { i.Path }));
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.ToString() });
            }
        }
        #endregion

        #region Download
        [HttpGet]
        [Route("dlna/tracker/download")]
        async public Task<JsonResult> Download(string path, int[] indexs, string thumb)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { error = "enable" });

            try
            {
                var tparse = await getTorrent(path);
                if (tparse.magnet == null)
                    return Json(new { error = "magnet" });

                // cache metadata
                string hash = Regex.Match(tparse.magnet, "btih:([a-z0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value.ToLower();
                if (IO.File.Exists($"cache/torrent/{hash}") && !IO.File.Exists($"cache/metadata/{hash.ToUpper()}.torrent"))
                    IO.File.Copy($"cache/torrent/{hash}", $"cache/metadata/{hash.ToUpper()}.torrent");
                
                var magnetLink = MagnetLink.Parse(tparse.magnet + (tparse.magnet.Contains("?") ? "&" : "?") + defTrackers);

                await bullderClientEngine();
                TorrentManager manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex() == magnetLink.InfoHashes.V1.ToHex());

                Directory.CreateDirectory($"{dlna_path}/");

                if (manager != null)
                {
                    if (manager.State is TorrentState.Stopped or TorrentState.Stopping or TorrentState.Error)
                    {
                        await manager.StartAsync();
                        await manager.WaitForMetadataAsync();

                        #region TorrentStateChanged
                        manager.TorrentStateChanged += async (s, e) =>
                        {
                            try
                            {
                                if (e != null && e.NewState == TorrentState.Seeding)
                                    await e.TorrentManager.StopAsync();

                                //if (e != null && e.NewState == TorrentState.Error)
                                //    await e.TorrentManager.StartAsync();

                                if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                                {
                                    try
                                    {
                                        IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHashes.V1.ToHex()}.torrent");
                                        IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHashes.V1.ToHex()}.json");
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

                                    await removeClientEngine(e.TorrentManager.InfoHashes.V1.ToHex().ToLower());
                                }
                            }
                            catch { }
                        };
                        #endregion
                    }

                    #region overideindexs
                    if (indexs != null && indexs.Length > 0)
                    {
                        var overideindexs = new List<int>();

                        for (int i = 0; i < manager.Files.Count; i++)
                        {
                            if (indexs.Contains(i) || manager.Files[i].Priority != Priority.DoNotDownload)
                                overideindexs.Add(i);
                        }

                        indexs = overideindexs.ToArray();
                    }
                    #endregion
                }
                else
                {
                    dynamic tlink = tparse.torrent != null ? Torrent.Load(tparse.torrent) : magnetLink;
                    manager = AppInit.conf.dlna.mode == "stream" ? await torrentEngine.AddStreamingAsync(tlink, $"{dlna_path}/") : await torrentEngine.AddAsync(tlink, $"{dlna_path}/");

                    #region AddTrackerAsync
                    if (IO.File.Exists("cache/trackers.txt") && AppInit.conf.dlna.addTrackersToMagnet)
                    {
                        foreach (string line in IO.File.ReadLines("cache/trackers.txt"))
                        {
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            string host = line.Replace("\r", "").Replace("\n", "").Replace("\t", "").Trim();

                            if (host.StartsWith("http") || host.StartsWith("udp:"))
                            {
                                try
                                {
                                    await manager.TrackerManager.AddTrackerAsync(new Uri(host));
                                }
                                catch { }
                            }
                        }
                    }
                    #endregion

                    await manager.StartAsync();
                    await manager.WaitForMetadataAsync();

                    #region overideindexs
                    if (indexs != null && indexs.Length > 0)
                    {
                        var overideindexs = new List<int>();

                        for (int i = 0; i < manager.Files.Count; i++)
                        {
                            if (indexs.Contains(i) || IO.File.Exists(manager.Files[i].FullPath))
                                overideindexs.Add(i);
                        }

                        indexs = overideindexs.ToArray();
                    }
                    #endregion

                    #region TorrentStateChanged
                    manager.TorrentStateChanged += async (s, e) =>
                    {
                        try
                        {
                            if (e != null && e.NewState == TorrentState.Seeding)
                                await e.TorrentManager.StopAsync();

                            //if (e != null && e.NewState == TorrentState.Error)
                            //    await e.TorrentManager.StartAsync();

                            if (e != null && (e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                            {
                                try
                                {
                                    IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHashes.V1.ToHex()}.torrent");
                                    IO.File.Delete($"cache/metadata/{e.TorrentManager.InfoHashes.V1.ToHex()}.json");
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

                                await removeClientEngine(e.TorrentManager.InfoHashes.V1.ToHex().ToLower());
                            }
                        }
                        catch { }
                    };
                    #endregion
                }

                #region indexs
                if (indexs == null || indexs.Length == 0)
                {
                    await manager.SetFilePriorityAsync(manager.Files[0], Priority.High);

                    if (manager.Files.Count > 1)
                    {
                        foreach (var file in manager.Files.Skip(1))
                        {
                            if (file.Priority != Priority.Normal)
                                await manager.SetFilePriorityAsync(file, Priority.Normal);
                        }
                    }
                }
                else
                {
                    Directory.CreateDirectory("cache/metadata/");
                    IO.File.WriteAllText($"cache/metadata/{manager.InfoHashes.V1.ToHex()}.json", JsonConvert.SerializeObject(indexs));

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
                            Directory.CreateDirectory($"{dlna_path}/thumbs");
                            IO.File.WriteAllBytes($"{dlna_path}/thumbs/{CrypTo.md5(manager.Torrent.Name)}.jpg", array);
                        }
                    }
                    catch { }
                }
                #endregion
            }
            catch (Exception ex)
            {
                await removeClientEngine();
                return Json(new { error = ex.ToString() });
            }

            return Json(new { status = true });
        }
        #endregion

        #region Delete
        [HttpGet]
        [Route("dlna/tracker/delete")]
        async public Task<JsonResult> TorrentDelete(string infohash)
        {
            if (!AppInit.conf.dlna.enable || torrentEngine == null)
                return Json(new { });

            var manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex() == infohash);
            if (manager != null)
            {
                try
                {
                    IO.File.Delete($"cache/metadata/{manager.InfoHashes.V1.ToHex()}.torrent");
                    IO.File.Delete($"cache/metadata/{manager.InfoHashes.V1.ToHex()}.json");
                }
                catch { }

                try
                {
                    await manager.StopAsync();
                }
                catch { }

                await removeClientEngine(manager.InfoHashes.V1.ToHex().ToLower());
            }

            return Json(new { status = true });
        }
        #endregion
    }
}