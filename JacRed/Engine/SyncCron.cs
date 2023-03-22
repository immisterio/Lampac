using Jackett;
using JacRed.Models.Details;
using JacRed.Models.Sync;
using Lampac.Engine.CORE;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace JacRed.Engine
{
    public static class SyncCron
    {
        static long lastsync = -1;

        async public static Task Run()
        {
            while (true)
            {
                try
                {
                    if (File.Exists(@"C:\ProgramData\lampac\disablesync"))
                        break;

                    if (!string.IsNullOrWhiteSpace(ModInit.conf.syncapi))
                    {
                        if (lastsync == -1 && File.Exists("cache/jacred/lastsync.txt"))
                            lastsync = long.Parse(File.ReadAllText("cache/jacred/lastsync.txt"));

                        var root = await HttpClient.Get<RootObject>($"{ModInit.conf.syncapi}/sync/fdb/torrents?time={lastsync}", timeoutSeconds: 300, MaxResponseContentBufferSize: 100_000_000);
                        if (root?.collections != null && root.collections.Count > 0)
                        {
                            foreach (var collection in root.collections)
                            {
                                bool updateMasterDb = false;

                                using (var fdb = FileDB.OpenWrite(collection.Key))
                                {
                                    foreach (var torrent in collection.Value.torrents)
                                    {
                                        if (torrent.Value.types == null || torrent.Value.types.Contains("sport"))
                                            continue;

                                        if (ModInit.conf.trackers != null && !ModInit.conf.trackers.Contains(torrent.Value.trackerName))
                                            continue;

                                        if (fdb.Database.TryGetValue(torrent.Key, out TorrentDetails val))
                                        {
                                            val = torrent.Value;
                                        }
                                        else
                                        {
                                            fdb.Database.TryAdd(torrent.Key, torrent.Value);
                                        }

                                        updateMasterDb = true;
                                    }
                                }

                                if (updateMasterDb)
                                {
                                    if (FileDB.masterDb.TryGetValue(collection.Key, out _))
                                    {
                                        FileDB.masterDb[collection.Key] = collection.Value.time;
                                    }
                                    else
                                    {
                                        FileDB.masterDb.TryAdd(collection.Key, collection.Value.time);
                                    }
                                }
                            }

                            lastsync = root.collections.Last().Value.time.ToFileTimeUtc();

                            if (root.nextread)
                                continue;
                        }

                        FileDB.SaveChangesToFile();
                        File.WriteAllText("cache/jacred/lastsync.txt", lastsync.ToString());
                    }
                }
                catch { }

                await Task.Delay(1000 * 60 * 60);
            }
        }
    }
}
