using Jackett;
using JacRed.Models.Sync;

namespace JacRed.Engine
{
    public static class SyncCron
    {
        static long lastsync = -1;

        async public static Task Run()
        {
            bool reset = true;
            await Task.Delay(TimeSpan.FromMinutes(2));

            DateTime lastSave = DateTime.Now;

            while (true)
            {
                try
                {
                    if (File.Exists(@"C:\ProgramData\lampac\disablesync"))
                        break;

                    if (ModInit.conf.typesearch == "red" && !string.IsNullOrWhiteSpace(ModInit.conf.Red.syncapi))
                    {
                        if (lastsync == -1 && File.Exists("cache/jacred/lastsync.txt"))
                            lastsync = long.Parse(File.ReadAllText("cache/jacred/lastsync.txt"));

                        var root = await Http.Get<RootObject>($"{ModInit.conf.Red.syncapi}/sync/fdb/torrents?time={lastsync}", timeoutSeconds: 300, MaxResponseContentBufferSize: 100_000_000, weblog: false);

                        if (root?.collections == null)
                        {
                            if (reset)
                            {
                                reset = false;
                                await Task.Delay(TimeSpan.FromMinutes(1));
                                continue;
                            }
                        }
                        else if (root.collections.Count > 0)
                        {
                            reset = true;
                            foreach (var collection in root.collections)
                            {
                                bool updateMasterDb = false;

                                using (var fdb = FileDB.Open(collection.Key, empty: true))
                                {
                                    foreach (var torrent in collection.Value.torrents)
                                    {
                                        if (torrent.Value.types == null || torrent.Value.types.Contains("sport"))
                                            continue;

                                        fdb.Database.AddOrUpdate(torrent.Key, torrent.Value, (k, v) => torrent.Value);
                                        updateMasterDb = true;
                                    }
                                }

                                if (updateMasterDb)
                                {
                                    if (FileDB.masterDb.ContainsKey(collection.Key))
                                    {
                                        FileDB.masterDb[collection.Key] = collection.Value.time;
                                    }
                                    else
                                    {
                                        FileDB.masterDb.TryAdd(collection.Key, collection.Value.time);
                                    }
                                }
                            }

                            lastsync = root.collections.Last().Value.fileTime;

                            if (root.nextread)
                            {
                                if (DateTime.Now > lastSave.AddMinutes(5))
                                {
                                    lastSave = DateTime.Now;
                                    FileDB.SaveChangesToFile();
                                    File.WriteAllText("cache/jacred/lastsync.txt", lastsync.ToString());
                                }

                                continue;
                            }
                        }

                        FileDB.SaveChangesToFile();
                        File.WriteAllText("cache/jacred/lastsync.txt", lastsync.ToString());
                    }
                    else
                    {
                        await Task.Delay(TimeSpan.FromMinutes(1));
                        continue;
                    }
                }
                catch
                {
                    try
                    {
                        if (lastsync > 0)
                        {
                            FileDB.SaveChangesToFile();
                            File.WriteAllText("cache/jacred/lastsync.txt", lastsync.ToString());
                        }
                    }
                    catch { }
                }

                await Task.Delay(1000 * Random.Shared.Next(60, 300));
                await Task.Delay(1000 * 60 * (20 > ModInit.conf.Red.syntime ? 20 : ModInit.conf.Red.syntime));

                reset = true;
                lastSave = DateTime.Now;
            }
        }
    }
}
