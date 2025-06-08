﻿using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Engine.Parse;
using Lampac.Models.DLNA;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;
using MonoTorrent;
using MonoTorrent.Client;
using NetVips;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Shared.Engine;
using Shared.Model.Online;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using IO = System.IO;

namespace Lampac.Controllers
{
    public class DLNAController : BaseController
    {
        static string mediaPattern = "(aac|flac|mpga|mpega|mp2|mp3|m4a|oga|ogg|opus|spx|opus|weba|wav|dif|dv|fli|mp4|mpeg|mpg|mpe|mpv|mkv|ts|m2ts|mts|ogv|webm|avi|qt|mov)";

        #region DLNAController
        static string dlna_path => AppInit.conf.dlna.path;

        static string defTrackers = "tr=http://retracker.local/announce&tr=http%3A%2F%2Fbt4.t-ru.org%2Fann%3Fmagnet&tr=http://retracker.mgts.by:80/announce&tr=http://tracker.city9x.com:2710/announce&tr=http://tracker.electro-torrent.pl:80/announce&tr=http://tracker.internetwarriors.net:1337/announce&tr=http://tracker2.itzmx.com:6961/announce&tr=udp://opentor.org:2710&tr=udp://public.popcorn-tracker.org:6969/announce&tr=udp://tracker.opentrackr.org:1337/announce&tr=http://bt.svao-ix.ru/announce&tr=udp://explodie.org:6969/announce&tr=wss://tracker.btorrent.xyz&tr=wss://tracker.openwebtorrent.com";

        static ClientEngine torrentEngine;

        public static void Initialization()
        {
            Directory.CreateDirectory("cache/torrent");
            Directory.CreateDirectory($"{dlna_path}/");
            Directory.CreateDirectory($"{dlna_path}/thumbs/");
            Directory.CreateDirectory($"{dlna_path}/tmdb/");

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                string trackers_best_ip = await HttpClient.Get("https://raw.githubusercontent.com/ngosang/trackerslist/master/trackers_best_ip.txt", timeoutSeconds: 20).ConfigureAwait(false);
                if (trackers_best_ip != null)
                {
                    foreach (string line in trackers_best_ip.Split("\n"))
                    {
                        string tr = line.Replace("\n", "").Replace("\r", "").Trim();
                        if (!string.IsNullOrWhiteSpace(tr))
                            defTrackers += $"&tr={tr}";
                    }
                }
            });

            ThreadPool.QueueUserWorkItem(async _ =>
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
                        await removeClientEngine();
                    }
                    catch { }
                }
            });

            if (!Directory.Exists("cache/metadata"))
                return;

            #region Resume download
            bullderClientEngine();

            foreach (string path in Directory.GetFiles("cache/metadata", "*.torrent"))
            {
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
            #endregion
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

            var sb = new StringBuilder(FileCache.ReadAllText("plugins/dlna.js"));

            sb.Replace("{localhost}", host)
              .Replace("{token}", HttpUtility.UrlEncode(token));

            return Content(sb.ToString(), "application/javascript; charset=utf-8");
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
                if (torrentEngine?.Torrents != null)
                {
                    var tdl = new List<TorrentManager>();

                    foreach (var i in torrentEngine.Torrents)
                    {
                        if (hash != null)
                        {
                            if (i.InfoHashes.V1.ToHex().ToLower() == hash)
                            {
                                try
                                {
                                    await i.StopAsync(TimeSpan.FromSeconds(20));
                                }
                                catch { }

                                tdl.Add(i);
                            }
                        }
                        else
                        {
                            if (i.State == TorrentState.Seeding || i.State == TorrentState.Stopped || i.State == TorrentState.Stopping)
                            {
                                try
                                {
                                    await i.StopAsync(TimeSpan.FromSeconds(120));
                                }
                                catch { }

                                tdl.Add(i);
                            }
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

                            try
                            {
                                await torrentEngine.RemoveAsync(item);
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

        #region getTmdb
        JObject getTmdb(string name)
        {
            try
            {
                string file = $"{dlna_path}/tmdb/{CrypTo.md5(name)}.json";
                if (IO.File.Exists(file))
                {
                    var tmdb = JsonConvert.DeserializeObject<JObject>(IO.File.ReadAllText(file));
                    tmdb.Remove("created_by");
                    tmdb.Remove("networks");
                    tmdb.Remove("production_companies");

                    return tmdb;
                }
            }
            catch { }

            return null;
        }
        #endregion

        #region getEpisodes
        JArray getEpisodes(JObject tmdb, string fileName)
        {
            try
            {
                if (tmdb == null || !tmdb.ContainsKey("number_of_seasons"))
                    return null;

                int season = getSeason(fileName);

                string file = $"{dlna_path}/tmdb/{tmdb.Value<long>("id")}_season-{season}.json";
                if (IO.File.Exists(file))
                {
                    if (memoryCache.TryGetValue(file, out JArray episodes))
                        return episodes;

                    episodes = JsonConvert.DeserializeObject<JObject>(IO.File.ReadAllText(file)).Value<JArray>("episodes");
                    
                    memoryCache.Set(file, episodes, DateTime.Now.AddSeconds(10));
                    return episodes;
                }
            }
            catch { }

            return null;
        }
        #endregion

        #region getEpisode
        int getEpisode(string fileName)
        {
            if (int.TryParse(Regex.Match(fileName, "EP?([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value, out int _e) && _e > 0)
                return _e;

            return 0;
        }
        #endregion

        #region getSeason
        int getSeason(string fileName)
        {
            if (int.TryParse(Regex.Match(fileName, "S([0-9]+)", RegexOptions.IgnoreCase).Groups[1].Value, out int _s) && _s > 0)
                return _s;

            return 0;
        }
        #endregion


        #region Navigation
        [HttpGet]
        [Route("dlna")]
        public ActionResult Index(string path)
        {
            if (!AppInit.conf.dlna.enable)
                return Json(new { });

            #region getImage
            string getImage(string name)
            {
                string pathimage = $"thumbs/{name}.jpg";
                if (IO.File.Exists($"{dlna_path}/" + pathimage))
                    return $"{host}/dlna/stream?path={HttpUtility.UrlEncode(pathimage)}";

                return null;
            }
            #endregion

            #region getPreview
            string getPreview(string name)
            {
                string pathimage = $"temp/{name}.mp4";
                if (IO.File.Exists($"{dlna_path}/" + pathimage))
                    return $"{host}/dlna/stream?path={HttpUtility.UrlEncode(pathimage)}";

                pathimage = $"temp/{name}.webm";
                if (IO.File.Exists($"{dlna_path}/" + pathimage))
                    return $"{host}/dlna/stream?path={HttpUtility.UrlEncode(pathimage)}";

                return null;
            }
            #endregion

            #region countFiles
            int countFiles(string _path)
            {
                int count = 0;

                foreach (string file in Directory.GetFiles(_path))
                {
                    if (!Regex.IsMatch(Path.GetExtension(file), mediaPattern))
                        continue;

                    if (new FileInfo(file).Length > 0)
                        count++;
                }

                return count;
            }
            #endregion

            var playlist = new List<DlnaModel>();

            #region folders
            foreach (string folder in Directory.GetDirectories($"{dlna_path}/" + path))
            {
                if (folder.Contains("thumbs") || folder.Contains("tmdb") || folder.Contains("temp"))
                    continue;

                int length = countFiles(folder);
                if (length > 0 || Directory.GetDirectories(folder).Length > 0)
                {
                    playlist.Add(new DlnaModel()
                    {
                        type = "folder",
                        name = Path.GetFileName(folder),
                        uri = $"{host}/dlna?path={HttpUtility.UrlEncode(folder.Replace($"{dlna_path}/", ""))}",
                        img = getImage(CrypTo.md5(Path.GetFileName(folder))),
                        preview = getPreview(CrypTo.md5(Path.GetFileName(folder))),
                        path = folder.Replace($"{dlna_path}/", ""),
                        length = countFiles(folder),
                        creationTime = Directory.GetCreationTime(folder),
                        tmdb = getTmdb(Path.GetFileName(folder))
                    });
                }
            }
            #endregion

            #region files
            var filesTmdb = getTmdb(path);
            var subtitles = Directory.GetFiles($"{dlna_path}/" + path, "*.srt");

            foreach (string file in Directory.GetFiles($"{dlna_path}/" + path))
            {
                if (!Regex.IsMatch(Path.GetExtension(file), mediaPattern))
                    continue;

                string name = Path.GetFileName(file);
                var fileinfo = new FileInfo(file);
                if (fileinfo.Length == 0)
                    continue;

                JObject episodeTmdb = null;

                string img = getImage(CrypTo.md5(name));
                var episodes = getEpisodes(filesTmdb, name);
                if (episodes != null)
                {
                    int episode = getEpisode(name);
                    if (episode > 0)
                    {
                        episodeTmdb = episodes.FirstOrDefault(i => i.Value<int>("episode_number") == episode)?.ToObject<JObject>();
                        episodeTmdb.Remove("crew");
                        episodeTmdb.Remove("guest_stars");

                        if (episodeTmdb != null && episodeTmdb.ContainsKey("still_path"))
                            img = $"tmdb:/t/p/w400" + episodeTmdb.Value<string>("still_path");
                    }
                }

                if (img == null)
                    img = getImage(CrypTo.md5($"{path}/{name}"));

                var dlnaModel = new DlnaModel()
                {
                    type = "file",
                    name = name,
                    uri = $"{host}/dlna/stream?path={HttpUtility.UrlEncode(file.Replace($"{dlna_path}/", ""))}",
                    img = img,
                    preview = getPreview(CrypTo.md5(name)),
                    subtitles = new List<Subtitle>(),
                    path = file.Replace($"{dlna_path}/", ""),
                    length = fileinfo.Length,
                    creationTime = fileinfo.CreationTime,
                    s = getSeason(name),
                    e = getEpisode(name),
                    tmdb = string.IsNullOrEmpty(path) ? getTmdb(name) : filesTmdb,
                    episode = episodeTmdb
                };

                #region subtitles
                foreach (string subfile in subtitles)
                {
                    if (subfile.Contains(Path.GetFileNameWithoutExtension(file)))
                    {
                        dlnaModel.subtitles.Add(new Subtitle()
                        {
                            label = "Sub #1",
                            url = $"{host}/dlna/stream?path={HttpUtility.UrlEncode($"{path}/{Path.GetFileName(subfile)}")}"
                        });
                    }
                }
                #endregion

                playlist.Add(dlnaModel);
            }
            #endregion

            var jSettings = new JsonSerializerSettings()
            {
                DefaultValueHandling = DefaultValueHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            };

            if (string.IsNullOrWhiteSpace(path))
            {
                #region torrentEngine
                if (torrentEngine?.Torrents != null)
                {
                    foreach (var t in torrentEngine.Torrents)
                    {
                        if (t.State == TorrentState.Metadata || t.State == TorrentState.Downloading || t.State == TorrentState.Starting)
                        {
                            if (t.Torrent?.Name == null || (!IO.File.Exists($"{dlna_path}/{t.Torrent.Name}") && !Directory.Exists($"{dlna_path}/{t.Torrent.Name}")))
                            {
                                playlist.Add(new DlnaModel()
                                {
                                    type = "file",
                                    name = t.Torrent?.Name ?? t.InfoHashes.V1.ToHex(),
                                    img = getImage(t.InfoHashes.V1.ToHex())
                                });
                            }
                        }
                    }
                }
                #endregion

                return ContentTo(JsonConvert.SerializeObject(playlist.OrderByDescending(i => i.creationTime), jSettings));
            }

            return ContentTo(JsonConvert.SerializeObject(playlist.OrderBy(i =>
            {
                ulong.TryParse(Regex.Replace(i.name, "[^0-9]+", ""), out ulong ident);
                return ident;

            }), jSettings));
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
            if (!AppInit.conf.dlna.enable || torrentEngine?.Torrents == null)
                return Content("[]");

            return Json(torrentEngine.Torrents.Select(i => new
            {
                InfoHash = i.InfoHashes.V1.ToHex(),
                Name = i.Torrent?.Name ?? i.InfoHashes.V1.ToHex(),
                //Engine = new 
                //{
                //    i.Engine.ConnectionManager.HalfOpenConnections,
                //    i.Engine.ConnectionManager.OpenConnections,
                //    i.Engine.TotalDownloadSpeed,
                //    i.Engine.TotalUploadSpeed,
                //},
                Files = i.Files?.Select(f => new
                {
                    f.Path,
                    Priority = f.Priority.ToString(),
                    f.Length,
                    BytesDownloaded = f.BytesDownloaded()
                }),
                i.Monitor,
                i.OpenConnections,
                i.PartialProgress,
                i.Progress,
                i.Peers,
                State = i.State.ToString(),
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
                s_cts.CancelAfter(1000 * 60 * 3);

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

                await bullderClientEngine();

                if (torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex().ToLower() == hash) is TorrentManager manager)
                {
                    if (manager.Files != null)
                        return Json(manager.Files.Select(i => (ITorrentFile)i).Select(i => new { i.Path }));

                    await manager.WaitForMetadataAsync(s_cts.Token);
                    var files = manager.Files.Select(i => (ITorrentFile)i);
                    return Json(files.Select(i => new { i.Path }));
                }

                var data = await torrentEngine.DownloadMetadataAsync(MagnetLink.Parse(magnet), s_cts.Token);
                if (data.IsEmpty)
                    return Json(new { error = "DownloadMetadata" });

                var array = data.Span.ToArray();
                IO.File.WriteAllBytes($"cache/torrent/{hash}", array);

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
        async public Task<JsonResult> Download(string path, int[] indexs, string thumb, long id, bool serial, int lastCount = -1)
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

                ThreadPool.QueueUserWorkItem(async _ =>
                {
                    try
                    {
                        #region Download thumb
                        if (thumb != null)
                        {
                            try
                            {
                                #region IsValidImg
                                bool IsValidImg(byte[] _img)
                                {
                                    if (AppInit.conf.imagelibrary == "NetVips")
                                        return IsValidImgage(_img, path);

                                    return true;
                                }
                                #endregion

                                string uri = Regex.Replace(thumb, "^https?://[^/]+/", "");

                                var array = await HttpClient.Download($"https://image.tmdb.org/{uri}", timeoutSeconds: 8).ConfigureAwait(false);
                                if (array == null || !IsValidImg(array))
                                    array = await HttpClient.Download($"https://imagetmdb.{AppInit.conf.cub.mirror}/{uri}").ConfigureAwait(false);

                                if (array != null && IsValidImg(array))
                                {
                                    Directory.CreateDirectory($"{dlna_path}/thumbs");
                                    IO.File.WriteAllBytes($"{dlna_path}/thumbs/{magnetLink.InfoHashes.V1.ToHex()}.jpg", array);
                                }
                            }
                            catch { }
                        }
                        #endregion

                        if (manager == null)
                        {
                            dynamic tlink = tparse.torrent != null ? Torrent.Load(tparse.torrent) : magnetLink;
                            manager = AppInit.conf.dlna.mode == "stream" ? await torrentEngine.AddStreamingAsync(tlink, $"{dlna_path}/") : await torrentEngine.AddAsync(tlink, $"{dlna_path}/");

                            #region AddTrackerAsync
                            if (IO.File.Exists("cache/trackers.txt") && AppInit.conf.dlna.addTrackersToMagnet)
                            {
                                foreach (string line in IO.File.ReadLines("cache/trackers.txt").OrderBy(x => Random.Shared.Next()))
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
                        }

                        await manager.StartAsync();
                        await manager.WaitForMetadataAsync();

                        #region thumb
                        if (IO.File.Exists($"{dlna_path}/thumbs/{magnetLink.InfoHashes.V1.ToHex()}.jpg"))
                        {
                            try
                            {
                                IO.File.Copy($"{dlna_path}/thumbs/{magnetLink.InfoHashes.V1.ToHex()}.jpg", $"{dlna_path}/thumbs/{CrypTo.md5(manager.Torrent.Name)}.jpg", true);
                            }
                            catch { }
                        }
                        #endregion

                        #region TorrentStateChanged
                        bool dispose = false;

                        manager.TorrentStateChanged += async (s, e) =>
                        {
                            try
                            {
                                if (!dispose && e != null && (e.NewState == TorrentState.Seeding || e.NewState == TorrentState.Stopped || e.NewState == TorrentState.Stopping))
                                {
                                    dispose = true;

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
                                            {
                                                if (f.Length == 0 || f.BytesDownloaded() == 0)
                                                {
                                                    IO.File.Delete(f.FullPath);
                                                }
                                                else
                                                {
                                                    double percentageDownloaded = (double)f.BytesDownloaded() / f.Length;

                                                    if (percentageDownloaded < 0.9)
                                                        IO.File.Delete(f.FullPath);
                                                }
                                            }
                                        }
                                        catch { }
                                    }

                                    await removeClientEngine(e.TorrentManager.InfoHashes.V1.ToHex().ToLower());
                                }
                            }
                            catch { }
                        };
                        #endregion

                        #region lastCount
                        if (lastCount > 0 && manager.Files.Count >= lastCount)
                        {
                            var _indexs = new List<int>();
                            for (int i = manager.Files.Count-1; i >= 0; i--)
                            {
                                if (_indexs.Count == lastCount)
                                    break;

                                if (!Regex.IsMatch(Path.GetExtension(manager.Files[i].Path), mediaPattern))
                                    continue;

                                _indexs.Add(i);
                            }

                            indexs = _indexs.ToArray();
                        }
                        #endregion

                        #region indexs
                        if (indexs == null || indexs.Length == 0)
                        {
                            foreach (var file in manager.Files)
                            {
                                if (file.Priority != Priority.Normal)
                                    await manager.SetFilePriorityAsync(file, Priority.Normal);
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
                                    if (manager.Files[i].Priority != Priority.Normal)
                                        await manager.SetFilePriorityAsync(manager.Files[i], Priority.Normal);
                                }
                                else
                                {
                                    if (manager.Files[i].Priority != Priority.DoNotDownload)
                                        await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
                                }
                            }
                        }
                        #endregion

                        #region tmdb
                        string cat = serial ? "tv" : "movie";
                        var header = HeadersModel.Init(("localrequest", IO.File.ReadAllText("passwd")));
                        string json = await HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/tmdb/api/3/{cat}/{id}?api_key={AppInit.conf.tmdb.api_key}&language=ru", timeoutSeconds: 20, headers: header);

                        if (string.IsNullOrEmpty(json))
                            json = await HttpClient.Get($"https://apitmdb.{AppInit.conf.cub.mirror}/3/{cat}/{id}?api_key={AppInit.conf.tmdb.api_key}&language=ru", timeoutSeconds: 20);

                        if (!string.IsNullOrEmpty(json))
                        {
                            IO.File.WriteAllText($"{dlna_path}/tmdb/{CrypTo.md5(manager.Torrent.Name)}.json", json);

                            if (serial)
                            {
                                if (int.TryParse(Regex.Match(json, "\"number_of_seasons\":([0-9 ]+)").Groups[1].Value.Trim(), out int number_of_seasons) && number_of_seasons > 0)
                                {
                                    async ValueTask write(int s)
                                    {
                                        string seasons = await HttpClient.Get($"http://{AppInit.conf.localhost}:{AppInit.conf.listenport}/tmdb/api/3/{cat}/{id}/season/{s}?api_key={AppInit.conf.tmdb.api_key}&language=ru", timeoutSeconds: 20, headers: header);

                                        if (string.IsNullOrEmpty(seasons))
                                            seasons = await HttpClient.Get($"https://apitmdb.{AppInit.conf.cub.mirror}/3/{cat}/{id}/season/{s}?api_key={AppInit.conf.tmdb.api_key}&language=ru", timeoutSeconds: 20);

                                        if (!string.IsNullOrEmpty(seasons))
                                            IO.File.WriteAllText($"{dlna_path}/tmdb/{id}_season-{s}.json", json);
                                    }

                                    if (number_of_seasons == 1)
                                        await write(number_of_seasons);
                                    else
                                    {
                                        foreach (var f in manager.Files)
                                        {
                                            int s = getSeason(Path.GetFileName(f.Path));
                                            if (s > 0)
                                                await write(s);
                                        }
                                    }
                                }
                            }
                        }
                        #endregion
                    }
                    catch { }
                });
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.ToString() });
            }

            return Json(new { status = true });
        }
        #endregion


        #region Delete
        [HttpGet]
        [Route("dlna/tracker/stop")]
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

        #region Pause
        [HttpGet]
        [Route("dlna/tracker/pause")]
        async public Task<JsonResult> TorrentPause(string infohash)
        {
            if (!AppInit.conf.dlna.enable || torrentEngine == null)
                return Json(new { });

            var manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex() == infohash);
            if (manager != null)
                await manager.PauseAsync();

            return Json(new { status = true });
        }
        #endregion

        #region Start
        [HttpGet]
        [Route("dlna/tracker/start")]
        async public Task<JsonResult> TorrentStart(string infohash)
        {
            if (!AppInit.conf.dlna.enable || torrentEngine == null)
                return Json(new { });

            var manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex() == infohash);
            if (manager != null)
                await manager.StartAsync();

            return Json(new { status = true });
        }
        #endregion

        #region ChangeFilePriority
        [HttpGet]
        [Route("dlna/tracker/changefilepriority")]
        async public Task<JsonResult> ChangeFilePriority(string infohash, int[] indexs)
        {
            if (!AppInit.conf.dlna.enable || torrentEngine == null)
                return Json(new { });

            var manager = torrentEngine.Torrents.FirstOrDefault(i => i.InfoHashes.V1.ToHex() == infohash);
            if (manager != null)
            {
                if (indexs == null || indexs.Length == 0)
                {
                    foreach (var file in manager.Files)
                    {
                        if (file.Priority != Priority.Normal)
                            await manager.SetFilePriorityAsync(file, Priority.Normal);
                    }

                    if (IO.File.Exists($"cache/metadata/{manager.InfoHashes.V1.ToHex()}.json"))
                        IO.File.Delete($"cache/metadata/{manager.InfoHashes.V1.ToHex()}.json");
                }
                else
                {
                    Directory.CreateDirectory("cache/metadata/");
                    IO.File.WriteAllText($"cache/metadata/{manager.InfoHashes.V1.ToHex()}.json", JsonConvert.SerializeObject(indexs));

                    for (int i = 0; i < manager.Files.Count; i++)
                    {
                        if (indexs.Contains(i))
                        {
                            if (manager.Files[i].Priority != Priority.Normal)
                                await manager.SetFilePriorityAsync(manager.Files[i], Priority.Normal);
                        }
                        else
                        {
                            if (manager.Files[i].Priority != Priority.DoNotDownload)
                                await manager.SetFilePriorityAsync(manager.Files[i], Priority.DoNotDownload);
                        }
                    }
                }
            }

            return Json(new { status = true });
        }
        #endregion



        #region IsValidImgage
        static bool IsValidImgage(byte[] _img, string path)
        {
            if (_img == null)
                return false;

            using (var image = Image.NewFromBuffer(_img))
            {
                try
                {
                    if (!path.Contains(".svg"))
                    {
                        // тестируем jpg/png на целостность
                        byte[] temp = image.JpegsaveBuffer();
                        if (temp == null || temp.Length == 0)
                            return false;
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }
        #endregion
    }
}